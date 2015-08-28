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
        public Projection()
        {
            Become(Uninitialized);
        }

        IActorRef _supervisor;
        protected int Sequence { get; set; }
        protected ProjectionState CurrentState { get; private set; } = new ProjectionState();

        Guid _replayId;
        Dictionary<Type, Action<IStreamEvent>> _eventHandlers = new Dictionary<Type, Action<IStreamEvent>>();
        ILoggingAdapter Log = Context.GetLogger();
        public IStash Stash { get; set; }
        protected IStreamEvent CurrentEvent { get; private set; }

        private void Uninitialized()
        {
            Receive<InitializeProjection>(async ini =>
            {
                _supervisor = ini.Supervisor;

                var state = await GetCurrentState();
                var query = BuildQuery();

                if (query == null)
                {
                    Log.Error("The projection can't start because the query is not defined.");
                    Context.Stop(Self);
                    return;
                }

                _supervisor.Tell(new ReplayProjectionRequest
                {
                    Sequence = state.Sequence,
                    SequenceHash = state.Hash,
                    Checkpoint = state.Checkpoint,
                });

                Become(Replaying);
            });
        }

        private void Replaying()
        {
            Receive<ReplayProjectionEvent>(e =>
            {
                var expected = Sequence + 1;
                var received = e.ProjectionEvent.Sequence;
                
                if (received == expected)
                {
                    ProcessEventInternal(e.ProjectionEvent);
                    Stash.UnstashAll();
                    return;
                }

                if (received < expected)
                {
                    Log.Error("Shouldn't be receiving duplicated events from replay...");
                    return;
                }

                if (received > expected)
                {
                    Stash.Stash();
                    return;
                }
            }, e => e.ReplayID == _replayId);

            Receive<InconsistentProjection>(async _ =>
            {
                _replayId = Guid.NewGuid();
                await PrepareToRebuild();
                

            }, e => e.ReplayID == _replayId);
        }

        private void ProcessEventInternal(IProjectionStreamEvent e)
        {
            Sequence = e.Sequence;
        }

        /// <summary>
        /// Returns the current state of the projection for recovery.
        /// </summary>
        protected virtual Task<ProjectionState> GetCurrentState()
        {
            return Task.FromResult(new ProjectionState());
        }

        protected virtual EventStoreQuery BuildQuery()
        {
            return null;
        }

        /// <summary>
        /// Runs when the projection is ready (finished rebuilding/replaying)
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

        public void ProcessEvent<T>(Action<IStreamEvent> processor)
        {
            Contract.Requires(processor != null);
            _eventHandlers.Add(typeof(T), processor);
        }

        public void ProcessEvent<T>(Action<IStreamEvent, T> processor)
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
