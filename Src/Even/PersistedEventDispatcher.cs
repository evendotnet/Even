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
        TimeSpan _recoveryTimeout;

        long _currentGlobalSequence;
        long _firstSequenceAfterGap;
        ICancelable _recovery;
        Guid _replayId;
        ILoggingAdapter _log;

        public EventDispatcher()
        {
            Become(Uninitialized);
        }

        void Uninitialized()
        {
            Receive<InitializeEventDispatcher>(ini =>
            {
                try
                {
                    Argument.Requires(ini.Reader != null);
                    Argument.Requires(ini.RecoveryStartTimeout > TimeSpan.Zero && ini.RecoveryStartTimeout < TimeSpan.FromMinutes(1));

                    _reader = ini.Reader;
                    _recoveryTimeout = ini.RecoveryStartTimeout;

                    _replayId = Guid.NewGuid();
                    _reader.Tell(new GlobalSequenceRequest { ReplayID = _replayId });
                    Become(AwaitingGlobalSequence);

                    Sender.Tell(InitializationResult.Successful());
                }
                catch (Exception ex)
                {
                    Sender.Tell(InitializationResult.Failed(ex));
                    Context.Stop(Self);
                }
            });
        }

        void AwaitingGlobalSequence()
        {
            Receive<GlobalSequenceResponse>(res =>
            {
                _currentGlobalSequence = res.LastGlobalSequence;
                Stash.UnstashAll();
                Become(Ready);

            }, r => r.ReplayID == _replayId);

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

                    // publish to the event stream and increment the sequence
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
                        _recovery = Context.System.Scheduler.ScheduleTellOnceCancelable(_recoveryTimeout, Self, new RecoverCommand(), Self);
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

                _replayId = Guid.NewGuid();

                _reader.Tell(new EventReplayRequest
                {
                    InitialGlobalSequence = initialSequence,
                    Count = count,
                    ReplayID = _replayId
                });

                Become(Recovering);
            });
        }

        void Recovering()
        {
            Receive<ReplayEvent>(e =>
            {

            }, e => e.ReplayID == _replayId);

            Receive<ReplayCompleted>(e =>
            {
                Stash.UnstashAll();
                Become(Ready);
            });

            ReceiveAny(_ => Stash.Stash());
        }

        class RecoverCommand { }
    }
}
