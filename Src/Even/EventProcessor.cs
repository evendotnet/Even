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
    public class EventProcessor : ReceiveActor, IWithUnboundedStash
    {
        public IStash Stash { get; set; }
        protected int CurrentSequence { get; private set; }
        protected string CurrentStreamID { get; private set; }

        Dictionary<Type, Func<IProjectionEvent, Task>> _eventProcessors = new Dictionary<Type, Func<IProjectionEvent, Task>>();

        ILoggingAdapter Log = Context.GetLogger();

        IActorRef _streams;
        Guid _replayId;
        string _projectionId;

        public EventProcessor()
        {
            Become(Uninitialized);
        }

        #region Actor States

        private void Uninitialized()
        {
            Receive<InitializeEventProcessor>(async ini =>
            {
                _streams = ini.ProjectionStreamSupervisor;

                var query = BuildQuery();
                CurrentStreamID = query.ProjectionStreamID;

                if (query == null)
                {
                    Log.Error("The projection can't start because the query is not defined.");
                    Context.Stop(Self);
                    return;
                }

                _projectionId = query.ProjectionStreamID;

                var knownState = await GetLastKnownState();

                // if the projection is new or id is changed, the projection need to be rebuilt
                if (knownState == null || !query.ProjectionStreamID.Equals(knownState.ProjectionStreamID, StringComparison.OrdinalIgnoreCase))
                    await PrepareToRebuild();

                _replayId = Guid.NewGuid();

                _streams.Tell(new ProjectionSubscriptionRequest
                {
                    ReplayID = _replayId,
                    Query = query,
                    LastKnownSequence = knownState?.ProjectionSequence ?? 0
                });

                Become(Replaying);
            });
        }

        private void Replaying()
        {
            Log.Debug("{0}: Starting Projection Replay", GetType().Name);

            Receive<ProjectionReplayEvent>(async e =>
            {
                var expected = CurrentSequence + 1;
                var received = e.Event.ProjectionSequence;
                
                if (received == expected)
                {
                    CurrentSequence = received;
                    await ProcessEventInternal(e.Event);
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
            Receive<IProjectionEvent>(async e =>
            {
                
                var expected = CurrentSequence + 1;
                var received = e.ProjectionSequence;

                if (received == expected)
                {
                    CurrentSequence++;
                    await ProcessEventInternal(e);
                    Stash.UnstashAll();
                    return;
                }

                if (received > expected)
                {
                    Stash.Stash();
                    return;
                }

            }, e => e.ProjectionStreamID == _projectionId);

            OnReady();
        }

        #endregion

        async Task ProcessEventInternal(IProjectionEvent e)
        {
            OnReceiveEvent(e);

            Func<IProjectionEvent, Task> processor;

            if (_eventProcessors.TryGetValue(e.DomainEvent.GetType(), out processor))
                await processor(e);
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

        private ProjectionStreamQuery BuildQuery()
        {
            var predicates = _eventProcessors.Keys.Select(t => new DomainEventPredicate(t)).ToList();

            return new ProjectionStreamQuery(predicates);
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

        #region Event Processor Registration

        protected void OnEvent<T>(Func<IPersistedEvent, Task> processor)
        {
            _eventProcessors.Add(typeof(T), e => processor(e));
        }

        protected void OnEvent<T>(Func<IPersistedEvent, T, Task> processor)
        {
            _eventProcessors.Add(typeof(T), e => processor(e, (T)e.DomainEvent));
        }

        protected void OnEvent<T>(Action<IPersistedEvent> processor)
        {
            _eventProcessors.Add(typeof(T), e =>
            {
                processor(e);
                return Task.CompletedTask;
            });
        }

        protected void OnEvent<T>(Action<IPersistedEvent, T> processor)
        {
            _eventProcessors.Add(typeof(T), e =>
            {
                processor(e, (T)e.DomainEvent);
                return Task.CompletedTask;
            });
        }

        #endregion
    }

    public class ProjectionReplayState
    {
        public int? LastSeenCheckpoint { get; set; }
        public int LastSeenSequence { get; set; }
        public int LastSeenHash { get; set; }
    }
}
