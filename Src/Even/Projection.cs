using Akka.Actor;
using Akka.Event;
using Even.Messages;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Threading.Tasks;

namespace Even
{
    public class Projection : ReceiveActor, IWithUnboundedStash
    {
        public IStash Stash { get; set; }
        protected int CurrentSequence { get; private set; }
        protected string CurrentStreamID { get; private set; }

        List<IStreamPredicate> _predicates = new List<IStreamPredicate>();
        Dictionary<Type, Action<IProjectionEvent>> _eventHandlers = new Dictionary<Type, Action<IProjectionEvent>>();

        ILoggingAdapter Log = Context.GetLogger();

        IActorRef _streams;
        Guid _replayId;
        string _projectionId;

        public Projection()
        {
            Become(Uninitialized);
        }

        private void Uninitialized()
        {
            Receive<InitializeProjection>(async ini =>
            {
                _streams = ini.Streams;

                var query = BuildQuery();
                CurrentStreamID = query.StreamID;

                if (query == null)
                {
                    Log.Error("The projection can't start because the query is not defined.");
                    Context.Stop(Self);
                    return;
                }

                _projectionId = query.StreamID;

                var knownState = await GetLastKnownState();

                // if the projection is new or id is changed, the projection need to be rebuilt
                if (knownState == null || !query.StreamID.Equals(knownState.StreamID, StringComparison.OrdinalIgnoreCase))
                    await PrepareToRebuild();

                _replayId = Guid.NewGuid();

                _streams.Tell(new ProjectionSubscriptionRequest
                {
                    ReplayID = _replayId,
                    Query = query,
                    LastKnownSequence = knownState?.Sequence ?? 0
                });

                Become(Replaying);
            });
        }

        private void Replaying()
        {
            Log.Debug("{0}: Starting Projection Replay", GetType().Name);

            Receive<ProjectionReplayEvent>(e =>
            {
                var expected = CurrentSequence + 1;
                var received = e.Event.ProjectionSequence;
                
                if (received == expected)
                {
                    ProcessEventInternal(e.Event);
                    Stash.UnstashAll();
                    return;
                }

                if (received > expected)
                    Stash.Stash();

            }, e => e.ReplayID == _replayId);

            Receive<ProjectionReplayCompleted>(e =>
            {
                Log.Debug("{0}: Projection Replay Completed", GetType().Name);

                var expected = CurrentSequence;
                var received = e.LastSequence;

                if (received == expected)
                {
                    Become(Ready);
                    return;
                }

                if (expected > received)
                {
                    Stash.Stash();
                    return;
                }

            }, e => e.ReplayID == _replayId);

            Receive<ReplayAborted>(e =>
            {
                throw new Exception("Replay Aborted");
            },
            e => e.ReplayID == _replayId);

            Receive<ReplayCancelled>(e =>
            {
                throw new Exception("Replay Aborted");
            },
            e => e.ReplayID == _replayId);  
        }

        void Ready()
        {
            // receive projection events
            Receive<IProjectionEvent>(e =>
            {
                
                var expected = CurrentSequence + 1;
                var received = e.ProjectionSequence;

                if (received == expected)
                {
                    CurrentSequence++;
                    ProcessEventInternal(e);
                    Stash.UnstashAll();
                    return;
                }

                if (received > expected)
                {
                    Stash.Stash();
                    return;
                }

            }, e => e.ProjectionID == _projectionId);

            OnReady();
        }

        private void ProcessEventInternal(IProjectionEvent e)
        {
            OnReceiveEvent(e);

            Action<IProjectionEvent> processor;

            if (_eventHandlers.TryGetValue(e.DomainEvent.GetType(), out processor))
                processor(e);
        }

        /// <summary>
        /// Returns the lask known state for the projection.
        /// If you're storing the state in an external store, you should
        /// override this method and read the state from there.
        /// </summary>
        protected virtual Task<ProjectionState> GetLastKnownState()
        {
            return Task.FromResult<ProjectionState>(null);
        }

        private ProjectionQuery BuildQuery()
        {
            return new ProjectionQuery(_predicates);
        }

        /// <summary>
        /// Runs when the projection is ready (finished rebuilding/replaying).
        /// You may use this to register receive handlers to the projection.
        /// </summary>
        protected virtual void OnReady()
        { }

        protected virtual void OnReceiveEvent(IProjectionEvent e)
        { }

        /// <summary>
        /// Signals the projection to prepare for rebuild.
        /// </summary>
        protected virtual Task PrepareToRebuild()
        {
            return Task.CompletedTask;
        }

        public void OnEvent<T>(Action<IEvent> processor)
        {
            Contract.Requires(processor != null);
            _eventHandlers.Add(typeof(T), processor);

            _predicates.Add(new TypedEventQuery<T>());
        }

        public void OnEvent<T>(Action<IEvent, T> processor)
        {
            OnEvent<T>(se => processor(se, (T)se.DomainEvent));
        }
    }

    public class ProjectionReplayState
    {
        public int? LastSeenCheckpoint { get; set; }
        public int LastSeenSequence { get; set; }
        public int LastSeenHash { get; set; }
    }
}
