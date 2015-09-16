using Akka.Actor;
using Akka.Event;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Even.Messages;

namespace Even
{
    public abstract class Aggregate : CommandProcessorBase, IWithUnboundedStash
    {
        public IStash Stash { get; set; }

        Guid _replayId;
        IActorRef _supervisor;
        IActorRef _reader;
        IActorRef _writer;
        PersistedEventHandler _eventProcessors = new PersistedEventHandler();
        LinkedList<UnpersistedEvent> _unpersistedEvents = new LinkedList<UnpersistedEvent>();
        PersistenceRequest _persistenceRequest;

        bool _snapshotNotAccepted;

        ILoggingAdapter _log = Context.GetLogger();

        protected bool IsNew => StreamSequence == 0;
        protected string StreamPrefix { get; private set; }
        protected string StreamID { get; private set; }
        protected int StreamSequence { get; private set; }
        protected bool IsReplaying { get; private set; } = true;

        // TODO: read these from settings
        static TimeSpan ReplayTimeout = TimeSpan.FromSeconds(30);

        protected override TimeSpan? IdleTimeout => TimeSpan.FromSeconds(30);

        protected override IActorRef ProcessorSupervisor => _supervisor;

        public Aggregate()
        {
            Become(Uninitialized);
        }

        #region Actor States

        private void Uninitialized()
        {
            Receive<InitializeAggregate>(ini =>
            {
                StreamPrefix = ESCategoryAttribute.GetCategory(this.GetType()) + "-";

                if (!IsValidStreamID(ini.StreamID))
                {
                    //TODO: this should reply some state to the supervisor, so it can refuse the first command
                    Sender.Tell(new AggregateInitializationState
                    {
                        Initialized = false,
                        InitializationFailureReason = "Invalid Stream"
                    });

                    Context.Stop(Self);
                    return;
                }

                _supervisor = ini.CommandProcessorSupervisor;
                _reader = ini.Reader;
                _writer = ini.Writer;
                StreamID = ini.StreamID;

                Sender.Tell(new AggregateInitializationState { Initialized = true });

                Become(AwaitingFirstCommand);
            });
        }

        void AwaitingFirstCommand()
        {
            Receive<CommandRequest>(r =>
            {
                // once the first command is received, stash it and start the replay
                Stash.Stash();

                _replayId = Guid.NewGuid();

                _reader.Tell(new ReplayAggregateRequest
                {
                    ReplayID = _replayId,
                    StreamID = StreamID,
                    InitialSequence = 1
                });

                Become(Replaying);

            }, r => String.Equals(r.StreamID, StreamID, StringComparison.OrdinalIgnoreCase));
        }
        
        void Replaying()
        {
            Receive<ReplayEvent>(async e =>
            {
                _log.Debug("Received Replay Event Sequence " + e.Event.StreamSequence);

                Contract.Assert(e.Event.StreamSequence == StreamSequence + 1, "Received event in wrong order");

                await ApplyEventInternal(e.Event);

            }, e => e.ReplayID == _replayId);

            Receive<ReplayCompleted>(_ =>
            {
                _log.Debug("Replay Completed");

                _replayId = Guid.Empty;

                IsReplaying = false;

                // remove the timeout handler
                SetReceiveTimeout(null);

                // switch to ready state and start receiving commands
                Become(Ready);

            }, msg => msg.ReplayID == _replayId);

            // on errors, let the actor restart
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

        void Ready()
        {
            _log.Debug("Ready to receive commands");

            Stash.UnstashAll();

            SetupBase();

            OnReady();

            // at this stage, simply skip any incoming replay messages
            Receive<ReplayMessage>(msg => { });
        }

        private void AwaitingPersistence()
        {
            _log.Debug("Awaiting persistence for " + _persistenceRequest.PersistenceID);
            
            Receive<IPersistedEvent>(async e =>
            {
                // locate the unpersisted node
                var node = _unpersistedEvents.Nodes().FirstOrDefault(n => n.Value.EventID == e.EventID);

                if (node != null)
                {
                    _unpersistedEvents.Remove(node);
                    await ApplyEventInternal(e);
                }

            }, e => e.StreamID.Equals(e.StreamID, StringComparison.OrdinalIgnoreCase));

            Receive<PersistenceSuccess>(_ =>
            {
                OnCommandSucceeded();
                AcceptCommand();
                Become(Ready);
                
            }, msg => msg.PersistenceID == _persistenceRequest.PersistenceID);

            // if there is a failure during persistence, try to restart and send the command to itself again
            Receive<PersistenceFailure>(failure =>
            {
                // forward the current command to itself again
                Self.Tell(new CommandRequest
                {
                    StreamID = StreamID,
                    CommandID = CurrentCommand.Request.CommandID,
                    Command = CurrentCommand.Request.Command,
                    Retries = CurrentCommand.Request.Retries + 1
                }, CurrentCommand.Sender);

                throw new Exception("Error persisting event", failure.Exception);

                return;
            });

            // TODO: SetReceiveTimeout

            ReceiveAny(_ => Stash.Stash());
        }

        #endregion

        #region Command Handler Responses

        /// <summary>
        /// Tells the aggregate to persist an event. Once the event is persisted,
        /// it is applied to the aggregate. You may call this command multiple times
        /// for a single command. No new commands are processed until all events
        /// from the same command are persisted.
        /// </summary>
        protected void Persist(object domainEvent)
        {
            Contract.Requires(domainEvent != null);
            _unpersistedEvents.AddLast(new UnpersistedEvent(StreamID, domainEvent));
        }

        #endregion

        internal override bool HandlePersistenceRequest()
        {
            if (_unpersistedEvents.Count == 0)
                return false;

            var request = new PersistenceRequest(StreamID, StreamSequence, _unpersistedEvents.ToList());

            _persistenceRequest = request;

            _writer.Tell(request);

            Become(AwaitingPersistence);

            return true;
        }

        protected abstract bool AcceptSnapshot(object snapshot);

        protected virtual bool IsValidStreamID(string streamId)
        {
            return streamId != null
                && streamId.Length > StreamPrefix.Length
                && streamId.StartsWith(StreamPrefix, StringComparison.OrdinalIgnoreCase);
        }

        protected virtual void OnReady()
        { }

        #region Event Processor Registration

        // on event

        protected void OnEvent<T>(Func<T, Task> processor)
        {
            _eventProcessors.AddHandler<T>(e => processor((T) e.DomainEvent));
        }

        protected void OnEvent<T>(Func<T, IPersistedEvent, Task> processor)
        {
            _eventProcessors.AddHandler<T>(e => processor((T)e.DomainEvent, e));
        }

        protected void OnEvent<T>(Action<T> processor)
        {
            _eventProcessors.AddHandler<T>(e => processor((T) e.DomainEvent));
        }

        protected void OnEvent<T>(Action<T, IPersistedEvent> processor)
        {
            _eventProcessors.AddHandler<T>(e => processor((T)e.DomainEvent, e));
        }

        #endregion

        private async Task ApplyEventInternal(IPersistedEvent e)
        {
            StreamSequence++;
            var eventType = e.DomainEvent.GetType();

            try {
                await OnReceiveEvent(e);
                await _eventProcessors.Handle(e);
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error processing agggregate event");
                throw;
            }
        }

        protected virtual Task OnReceiveEvent(IPersistedEvent e)
        {
            return Task.CompletedTask;
        }

        class PersistenceContext
        {
            public IActorRef CommandSender { get; set; }
            public CommandRequest Command { get; set; }
            public PersistenceRequest PersistenceRequest { get; set; }
        }
    }

    public abstract class Aggregate<TState> : Aggregate
    {
        protected TState State { get; set; }

        protected override bool AcceptSnapshot(object snapshot)
        {
            if (snapshot is TState)
            {
                State = (TState)snapshot;
                return true;
            }

            return false;
        }
    }
}
