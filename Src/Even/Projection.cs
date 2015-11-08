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
        protected string ProjectionStreamID { get; private set; }

        PersistedEventHandler _eventHandlers = new PersistedEventHandler();
        QueryHandler _queryHandlers = new QueryHandler();
        LinkedList<Type> _eventTypes = new LinkedList<Type>();

        IActorRef _streams;
        GlobalOptions _options;
        Guid _lastRequestId;

        IActorRef _querySender;

        public Projection()
        {
            Become(Uninitialized);
        }

        protected new IActorRef Sender => _querySender ?? base.Sender;

        #region Actor States

        private void Uninitialized()
        {
            Receive<InitializeProjection>(async ini =>
            {
                try
                {
                    _streams = ini.ProjectionStreams;
                    _options = ini.Options;

                    var query = BuildQuery();
                    ProjectionStreamID = query.ProjectionStreamID;

                    await OnInit();

                    var lastKnownState = await GetLastKnownState();

                    // if the projection is new or id is changed, the projection needs to be rebuilt
                    if (lastKnownState != null && !ProjectionStreamID.Equals(lastKnownState.ProjectionStreamID, StringComparison.OrdinalIgnoreCase))
                        await PrepareToRebuild();

                    var request = new ProjectionSubscriptionRequest(query, lastKnownState?.ProjectionSequence ?? 0);
                    _lastRequestId = request.RequestID;
                    _streams.Tell(request);

                    Become(Replaying);

                    Sender.Tell(InitializationResult.Successful());
                }
                catch (Exception ex)
                {
                    Sender.Tell(InitializationResult.Failed(ex));
                    throw;
                }
            });
        }

        private void Replaying()
        {
            SetReceiveTimeout(_options.ReadRequestTimeout);

            Receive<ProjectionReplayEvent>(async e =>
            {
                // test the request id here instead of the receive predicate,
                // so we can discard cancelled replay messages instead of stashing them
                if (e.RequestID != _lastRequestId)
                    return;

                var expected = CurrentSequence + 1;
                var received = e.Event.StreamSequence;

                if (expected != received)
                {
                    Sender.Tell(new CancelRequest(e.RequestID));
                    throw new EventOutOfOrderException(expected, received, "Unexpected event sequence replaying projection");
                }

                CurrentSequence++;
                await ProcessEventInternal(e.Event);
            });

            Receive<ProjectionReplayFinished>(e =>
            {
                if (e.RequestID != _lastRequestId)
                    return;

                Stash.UnstashAll();
                Become(Ready);
            });

            Receive<Aborted>(e =>
            {
                if (e.RequestID != _lastRequestId)
                    return;

                throw new Exception("Replay Aborted", e.Exception);
            });

            // cancelled messages can be ignored safely
            Receive<Cancelled>(_ => { });

            Receive(new Action<ReceiveTimeout>(_ =>
            {
                throw new TimeoutException("Timeout waiting for replay messages.");
            }));

            ReceiveAny(_ => Stash.Stash());
        }

        void Ready()
        {
            // receive projection events
            Receive<IPersistedStreamEvent>(async e =>
            {
                // skip old messages
                if (e.StreamSequence <= CurrentSequence)
                    return;

                if (e.StreamSequence > CurrentSequence + 1)
                    throw new EventOutOfOrderException(CurrentSequence + 1, e.StreamSequence, "Projection received an event out of order.");

                CurrentSequence++;
                await ProcessEventInternal(e);

            }, e => e.StreamID == ProjectionStreamID);

            Receive<IQuery>(async q =>
            {
                _querySender = q.Sender;

                var handled = await _queryHandlers.Handle(q);

                if (!handled)
                    Unhandled(q);

                _querySender = null;
            });

            Receive<RebuildProjection>(async _ =>
            {
                await PrepareToRebuild();
                throw new RebuildRequestException();
            });

            OnReady();
        }

        #endregion

        async Task ProcessEventInternal(IPersistedStreamEvent e)
        {
            await OnReceiveEvent(e);
            await _eventHandlers.Handle(e);
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

        protected virtual Task OnReceiveEvent(IPersistedStreamEvent e)
        {
            return Task.CompletedTask;
        }

        protected virtual Task OnExpiredQuery(object query)
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

        protected void OnEvent<T>(Func<IPersistedStreamEvent<T>, Task> handler)
        {
            var t = typeof(T);

            if (!_eventTypes.Contains(t))
                _eventTypes.AddLast(t);

            _eventHandlers.AddHandler<T>(e => handler((IPersistedStreamEvent<T>) e));
        }

        protected void OnEvent<T>(Action<IPersistedStreamEvent<T>> handler)
        {
            var t = typeof(T);

            if (!_eventTypes.Contains(t))
                _eventTypes.AddLast(t);

            _eventHandlers.AddHandler<T>(e => handler((IPersistedStreamEvent<T>)e));
        }

        #endregion

        #region Query Subscriptions

        protected void OnQuery<T>(Func<T, Task> handler)
        {
            Context.System.EventStream.Subscribe(Self, typeof(IQuery<T>));

            _queryHandlers.AddHandler<T>(q =>
            {
                if (q.Timeout.IsExpired)
                    return OnExpiredQuery(q.Message);

                return handler((T) q.Message);
            });
        }

        protected void OnQuery<T>(Action<T> handler)
        {
            OnQuery(new Func<T, Task>(q =>
            {
                handler(q);
                return Task.CompletedTask;
            }));
        }

        #endregion
    }

    public class ProjectionReplayState
    {
        [Obsolete]
        public int? LastSeenCheckpoint { get; set; }
        public int LastSeenSequence { get; set; }
        public int LastSeenHash { get; set; }
    }
}
