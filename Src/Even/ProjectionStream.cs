using Akka.Actor;
using Akka.Event;
using Even.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Even
{
    /// <summary>
    /// Ator responsável por um stream. Mantem a sequencia de eventos do stream e emite o evento para os subscribers.
    /// </summary>
    public class ProjectionStream : ReceiveActor, IWithUnboundedStash
    {
        public ProjectionStream()
        {
            Become(Uninitialized);
        }

        long _lastSeenCheckpoint;
        EventStoreQuery _query;
        IStreamPredicate[] _predicates;
        IActorRef _reader;
        List<IActorRef> _subscribers = new List<IActorRef>();
        ProjectionStreamState _state = new ProjectionStreamState();
        Guid _replayId;

        TimeSpan ReplayTimeout = TimeSpan.FromSeconds(15);

        public ILoggingAdapter Log = Context.GetLogger();
        public IStash Stash { get; set; }

        #region States

        private void Uninitialized()
        {
            Receive<InitializeProjectionStream>(ini =>
            {
                _query = ini.Query;
                _reader = ini.Reader;

                // subscribe to events in the stream
                Context.System.EventStream.Subscribe(Self, typeof(IStreamEvent));

                // start the replay
                _replayId = Guid.NewGuid();

                _reader.Tell(new ReplayQueryRequest
                {
                    ReplayID = _replayId,
                    Query = _query,
                    InitialCheckpoint = 0,
                    MaxEvents = Int32.MaxValue
                });

                Become(Replaying);
            });
        }

        private void Replaying()
        {
            Stash.UnstashAll();

            Receive<ReplayEvent>(e =>
            {
                // during replay, only update the projection state
                if (EventMatches(e.Event))
                    _state.AppendCheckpoint(e.Event.Checkpoint);

            }, e => e.ReplayID == _replayId);

            Receive<ReplayCompleted>(_ =>
            {
                _replayId = Guid.Empty;

                // remove the timeout handler
                SetReceiveTimeout(null);

                // switch to start receiving commands
                Become(Ready);

            }, msg => msg.ReplayID == _replayId);

            // on errors, let the stream restart
            Receive<ReplayCancelled>(msg =>
            {
                throw new Exception("Replay was cancelled.");

            }, msg => msg.ReplayID == _replayId);

            Receive<ReplayAborted>(msg =>
            {
                throw new Exception("Replay was aborted.", msg.Exception);

            }, msg => msg.ReplayID == _replayId);

            // if no messages are received for some time, abort
            SetReceiveTimeout(ReplayTimeout);

            Receive(new Action<ReceiveTimeout>(_ =>
            {
                throw new Exception("Timeout during replay.");
            }));

            // any other messages are stashed until this step is completed
            ReceiveAny(o => Stash.Stash());
        }

        private void Ready()
        {
            Receive<IStreamEvent>(e =>
            {
                if (EventMatches(e))
                    Emit(e);
            });

            Receive<ProjectionSubscriptionRequest>(ps =>
            {
                _subscribers.Add(Sender);
            }, ps => ps.Query.ID == _query.ID);

            Receive<ReplayProjectionRequest>(request =>
            {
                var props = Props.Create<ReplayWorker>(_reader);
                var worker = Context.ActorOf(props);
                worker.Forward(request);
            });
        }

        #endregion

        private bool EventMatches(IStreamEvent streamEvent)
        {
            return true;
        }

        protected virtual void Emit(IStreamEvent streamEvent)
        {
            _state.AppendCheckpoint(streamEvent.Checkpoint);
            var projectionEvent = new ProjectionStreamEvent(_query.ID, _state.Sequence, _state.SequenceHash, streamEvent);

            foreach (var s in _subscribers)
                s.Tell(projectionEvent);
        }

        class ReplayWorker : ReceiveActor
        {
            public ReplayWorker(IActorRef reader)
            {
                Receive<ReplayProjectionRequest>(request =>
                {
                    //TODO
                });
            }
        }
    }
}
