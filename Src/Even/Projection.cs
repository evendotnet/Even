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

        ProjectionEventHandler _handlers = new ProjectionEventHandler();
        LinkedList<Type> _eventTypes = new LinkedList<Type>();

        ILoggingAdapter Log = Context.GetLogger();

        IActorRef _streams;
        Guid _replayId;
        string _projectionId;

        public Projection()
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

                await OnInit();

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
                Contract.Assert(e.Event.ProjectionStreamSequence == CurrentSequence + 1);

                CurrentSequence++;
                await ProcessEventInternal(e.Event);

            }, e => e.ReplayID == _replayId);

            Receive<ProjectionReplayCompleted>(e =>
            {
                Log.Debug("{0}: Projection Replay Completed", GetType().Name);

                Contract.Assert(e.LastSeenProjectionStreamSequence == CurrentSequence);

                Become(Ready);

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
                Contract.Assert(e.ProjectionStreamSequence == CurrentSequence + 1);

                CurrentSequence++;
                await ProcessEventInternal(e);

            }, e => e.ProjectionStreamID == _projectionId);

            OnReady();
        }

        #endregion

        async Task ProcessEventInternal(IProjectionEvent e)
        {
            await OnReceiveEvent(e);
            await _handlers.Handle(e);
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
            var predicates = _eventTypes.Distinct().Select(t => new DomainEventPredicate(t)).ToList();

            return new ProjectionStreamQuery(predicates);
        }

        /// <summary>
        /// Runs when the projection is ready (finished rebuilding/replaying).
        /// You may use this to register receive handlers to the projection.
        /// </summary>
        protected virtual void OnReady()
        { }

        protected virtual Task OnInit()
        {
            return Task.CompletedTask;
        }

        protected virtual Task OnReceiveEvent(IProjectionEvent e)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Signals the projection to prepare for rebuild.
        /// </summary>
        protected virtual Task PrepareToRebuild()
        {
            return Task.CompletedTask;
        }

        #region Event Subscriptions

        protected void OnEvent<T>(Func<IProjectionEvent<T>, Task> handler)
        {
            var t = typeof(T);

            if (!_eventTypes.Contains(t))
                _eventTypes.AddLast(t);

            _handlers.AddHandler<T>(e => handler((IProjectionEvent<T>) e));
        }

        protected void OnEvent<T>(Action<IProjectionEvent<T>> handler)
        {
            var t = typeof(T);

            if (!_eventTypes.Contains(t))
                _eventTypes.AddLast(t);

            _handlers.AddHandler<T>(e => handler((IProjectionEvent<T>)e));
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
