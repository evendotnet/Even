using Akka.Actor;
using Akka.Event;
using Even.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even
{
    public class EventDispatcher : ReceiveActor, IWithUnboundedStash
    {
        public IStash Stash { get; set; }

        IActorRef _reader;
        GlobalOptions _options;

        long _currentGlobalSequence;
        long _firstSequenceAfterGap;
        ICancelable _recovery;
        Guid _requestId;

        ILoggingAdapter _log = Context.GetLogger();

        public static Props CreateProps(IActorRef reader, GlobalOptions options)
        {
            return Props.Create<EventDispatcher>(reader, options);
        }

        public EventDispatcher(IActorRef reader, GlobalOptions options)
        {
            Argument.RequiresNotNull(reader, nameof(reader));
            Argument.RequiresNotNull(options, nameof(options));

            _reader = reader;
            _options = options;

            var request = new ReadHighestGlobalSequenceRequest();
            _reader.Tell(request);
            _requestId = request.RequestID;

            Become(AwaitingGlobalSequence);

            AwaitingGlobalSequence();
        }
        
        void AwaitingGlobalSequence()
        {
            Receive<ReadHighestGlobalSequenceResponse>(res =>
            {
                _currentGlobalSequence = res.GlobalSequence;
                Stash.UnstashAll();
                Become(Ready);

            }, r => r.RequestID == _requestId);

            ReceiveAny(_ => Stash.Stash());
        }

        void Ready()
        {
            Receive<IPersistedEvent>(e =>
            {
                var received = e.GlobalSequence;
                var expected = _currentGlobalSequence + 1;

                // if the event is the expected one
                if (received == expected)
                {
                    _currentGlobalSequence++;

                    // publish to the event stream
                    Context.System.EventStream.Publish(e);

                    // if a recovery was requested, cancel as the event already arrived
                    if (_recovery != null)
                    {
                        _firstSequenceAfterGap = 0;
                        _recovery.Cancel();
                        _recovery = null;

                        Stash.UnstashAll();
                    }

                    return;
                }

                // if a gap is detected, stash and start the recovery timeout
                if (received > expected)
                {
                    if (_recovery == null)
                    {
                        _recovery = Context.System.Scheduler.ScheduleTellOnceCancelable(_options.DispatcherRecoveryTimeout, Self, new RecoverCommand(), Self);
                        _firstSequenceAfterGap = received;
                    }
                    else
                    {
                        _firstSequenceAfterGap = Math.Min(_firstSequenceAfterGap, received);
                    }

                    Stash.Stash();
                    return;
                }

                // just ignore events we've seen
                if (received < expected)
                {
                    _log.Debug("Received past event @" + received);
                    return;
                }
            });

            Receive<RecoverCommand>(_ =>
            {
                // if the recovery is null, it means the RecoverCommand was sent
                // but the expected event arrived right before it was cancelled, so just return
                if (_recovery == null)
                    return;

                var initialSequence = _currentGlobalSequence + 1;
                var count = (int) (_firstSequenceAfterGap - initialSequence);

                var request = new ReadRequest(initialSequence, count);
                _requestId = request.RequestID;
                _reader.Tell(request);

                Become(Recovering);
            });
        }

        void Recovering()
        {
            Receive<ReadResponse>(m =>
            {
                // publish to the event stream
                Context.System.EventStream.Publish(m.Event);

            }, e => e.RequestID == _requestId);

            Receive<ReadFinished>(m =>
            {
                _currentGlobalSequence = _firstSequenceAfterGap - 1;
                Stash.UnstashAll();
                Become(Ready);
            });

            ReceiveAny(_ => Stash.Stash());
        }

        class RecoverCommand { }
    }
}
