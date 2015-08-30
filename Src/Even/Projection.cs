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
        protected IEvent CurrentEvent { get; private set; }
        protected int CurrentSequence { get; private set; }
        protected string CurrentStreamID { get; private set; }

        Dictionary<Type, Action<IEvent>> _eventHandlers = new Dictionary<Type, Action<IEvent>>();
        ILoggingAdapter Log = Context.GetLogger();

        IActorRef _supervisor;
        Guid _replayId;

        public Projection()
        {
            Become(Uninitialized);
        }

        private void Uninitialized()
        {
            Receive<InitializeProjection>(async ini =>
            {
                _supervisor = ini.Supervisor;

                var query = BuildQuery();
                CurrentStreamID = query.StreamID;

                if (query == null)
                {
                    Log.Error("The projection can't start because the query is not defined.");
                    Context.Stop(Self);
                    return;
                }

                var knownState = await GetLastKnownState();

                // if the projection is new or id is changed, the projection need to be rebuilt
                if (knownState != null || !query.StreamID.Equals(knownState.StreamID, StringComparison.OrdinalIgnoreCase))
                    await PrepareToRebuild();

                _replayId = Guid.NewGuid();

                _supervisor.Tell(new ProjectionSubscriptionRequest
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
            Receive<IProjectionEvent>(e =>
            {

            });

            OnReady();
        }

        private void ProcessEventInternal(IProjectionEvent e)
        {
            CurrentSequence = e.ProjectionSequence;
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
            return null;
        }

        /// <summary>
        /// Runs when the projection is ready (finished rebuilding/replaying).
        /// You may use this to register receive handlers to the projection.
        /// </summary>
        protected virtual void OnReady()
        { }

        /// <summary>
        /// Signals the projection to prepare for rebuild.
        /// </summary>
        protected virtual Task PrepareToRebuild()
        {
            return Task.CompletedTask;
        }

        public void ProcessEvent<T>(Action<IEvent> processor)
        {
            Contract.Requires(processor != null);
            _eventHandlers.Add(typeof(T), processor);
        }

        public void ProcessEvent<T>(Action<IEvent, T> processor)
        {
            ProcessEvent<T>(se => processor(se, (T)se.DomainEvent));
        }
    }

    public class ProjectionReplayState
    {
        public int? LastSeenCheckpoint { get; set; }
        public int LastSeenSequence { get; set; }
        public int LastSeenHash { get; set; }
    }
}
