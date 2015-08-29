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
    public abstract class Aggregate : ReceiveActor, IWithUnboundedStash
    {
        public Aggregate()
        {
            Become(Uninitialized);
        }

        Guid _replayId;
        IActorRef _reader;
        IActorRef _writer;
        Dictionary<Type, Func<object, Task>> _commandProcessors = new Dictionary<Type, Func<object, Task>>();
        Dictionary<Type, Action<object>> _eventProcessors = new Dictionary<Type, Action<object>>();

        public IStash Stash { get; set; }
        protected ILoggingAdapter Log { get; } = Context.GetLogger();
        protected virtual string StreamID => _streamId;
        protected int Sequence { get; private set; }
        protected bool IsNew => Sequence == 0;
        protected string Category => _category;

        string _streamId;
        string _category;

        List<PersistenceRequest> _pendingEvents = new List<PersistenceRequest>();
        List<Guid> _optimisticPersistRequests = new List<Guid>();
        int _persistSequence;
        CommandContext _commandContext;

        static TimeSpan ReplayTimeout = TimeSpan.FromSeconds(10);

        #region Actor States

        private void Uninitialized()
        {
            _category = ESCategoryAttribute.GetCategory(this.GetType());

            Receive<InitializeAggregate>(i =>
            {
                // ensure the stream is valid before initializing the aggregate
                if (!CanAcceptStream(i.StreamID))
                {
                    RefuseInvalidStream(i.Command, i.CommandSender);
                    Context.Stop(Self);
                }

                _streamId = i.StreamID;
                _reader = i.Reader;
                _writer = i.Writer;

                _replayId = Guid.NewGuid();

                _reader.Tell(new ReplayAggregateRequest
                {
                    ReplayID = _replayId,
                    StreamID = i.StreamID
                });

                Become(Replaying);
            });
        }

        private void Replaying()
        {
            Stash.UnstashAll();

            //TODO: implement snapshot recovery

            Receive<ReplayEvent>(e =>
            {
                Log.Debug("Received Replay Event Sequence " + e.Event.StreamSequence);

                var received = e.Event.StreamSequence;
                var expected = Sequence + 1;

                if (received == expected)
                {
                    OnProcessEvent(e.Event);
                    Stash.UnstashAll();
                    return;
                }

                if (received < expected)
                    return;

                if (received > expected)
                    Stash.Stash();

            }, e => e.ReplayID == _replayId);

            Receive<ReplayCompleted>(_ =>
            {
                Log.Debug("Replay Completed");

                _replayId = Guid.Empty;

                // remove the timeout handler
                SetReceiveTimeout(null);

                // switch to start receiving commands
                Become(ReceivingCommands);

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

        protected void ReceivingCommands()
        {
            Log.Debug("Ready to receive commands");

            Stash.UnstashAll();

            Receive<AggregateCommandRequest>(cmd =>
            {
                // ensure the stream is valid before accepting
                if (!CanAcceptStream(cmd.StreamID))
                {
                    RefuseInvalidStream(cmd, Sender);
                    return;
                }

                Log.Debug("Command {0} Received: {1}", cmd.CommandID, cmd.Command.GetType().Name);

                var commandType = cmd.Command.GetType();
                Func<object, Task> processor = null;

                foreach (var kvp in _commandProcessors)
                {
                    if (kvp.Key == commandType)
                    {
                        processor = kvp.Value;
                        break;
                    }
                }

                if (processor == null)
                {
                    Sender.Tell(new AggregateCommandRefused
                    {
                        CommandID = cmd.CommandID,
                        CommandRequest = cmd
                    });

                    return;
                }

                // reset the persistence sequence
                _persistSequence = Sequence;

                // sets the context so we can send messages to the sender after persistence
                _commandContext = new CommandContext { Sender = Sender, CommandID = cmd.CommandID };

                AkkaAsyncHelper.Await(async () => {
                    try
                    {
                        await processor(cmd.Command);

                        // if there are no pending events, no persistence happened and the command was successful with no actions
                        if (_pendingEvents.Count == 0)
                        {
                            _commandContext.Sender.Tell(new AggregateCommandSuccessful { CommandID = cmd.CommandID });
                            Log.Debug("Commnad {0} Succeeded with no actions", cmd.CommandID);
                        }
                    }
                    catch (Exception ex)
                    {
                        _commandContext.Sender.Tell(new AggregateCommandFailed { CommandID = cmd.CommandID, Exception = ex });
                        Log.Debug("Commnad {0} Failed: {1}", cmd.CommandID, ex.Message);
                    }
                });

            });

            // if any persistence fails for optimistic persistences,
            // restart the actor so it can replay whatever is in the store
            Receive(new Action<PersistenceFailed>(msg =>
            {
                throw new Exception("Optimistic Persistence Error");
            }), msg => _optimisticPersistRequests.Contains(msg.EventID));

            // at this stage, simply skip any incoming replay messages
            Receive<ReplayMessage>(msg => { });
        }

        private void AwaitingPersistence()
        {
            Log.Debug("Awaiting persistence for " + _commandContext.CommandID);

            Receive<PersistenceSuccessful>(msg =>
            {
                var index = _pendingEvents.FindIndex(e => e.EventID == msg.EventID);
                var request = _pendingEvents[index];

                _pendingEvents.RemoveAt(index);

                OnProcessEvent(request.DomainEvent);

                // send confirmation to the source
                _commandContext.Sender.Tell(new AggregateCommandSuccessful { CommandID = _commandContext.CommandID });

                Log.Debug("Command {0} succeeded after persistence confirmation", _commandContext.CommandID);

                // if no more events are pending, resume accepting commands
                if (_pendingEvents.Count == 0)
                    Become(ReceivingCommands);

            }, msg => _pendingEvents.Any(r => r.EventID == msg.EventID));

            // if the persistence fails for any reason, restart the actor
            // this will currently lose the event
            Receive(new Action<PersistenceFailed>(_ =>
            {
                //TODO: should probably give some reason on why the persistence failed
                _commandContext.Sender.Tell(new AggregateCommandFailed { CommandID = _commandContext.CommandID });

                throw new Exception("Persistence failed.");
            }));

            // TODO: SetReceiveTimeout for command timeouts?

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

            var request = new PersistenceRequest
            {
                EventID = Guid.NewGuid(),
                StreamID = StreamID,
                StreamSequence = _persistSequence++,
                DomainEvent = domainEvent
            };

            _pendingEvents.Add(request);
            _writer.Tell(request);

            Become(AwaitingPersistence);
        }

        /// <summary>
        /// Tells the aggregate to persist an event and applies the event immediatelly.
        /// This method does not guarantee the event was persisted to the event store before
        /// processing it. If any persistence errors are received after this command, the
        /// aggregate is restarted to ensure consistence but some events may be lost.
        /// Use this method if you don't care 
        /// </summary>
        protected void OptimisticPersist(object domainEvent)
        {
            Contract.Requires(domainEvent != null);

            var request = new PersistenceRequest
            {
                EventID = Guid.NewGuid(),
                StreamID = StreamID,
                StreamSequence = _persistSequence,
                DomainEvent = domainEvent
            };

            _optimisticPersistRequests.Add(request.EventID);
            _writer.Tell(request);
            OnProcessEvent(domainEvent);
        }

        /// <summary>
        /// Causes the command to interrupt processing imediatelly and reply to the sender.
        /// </summary>
        protected void Fail(string message)
        {
            throw new Exception(message);
        }

        #endregion

        #region Process Helpers

        protected void ProcessCommand<T>(Action<T> action)
        {
            _commandProcessors.Add(typeof(T), o => {
                action((T)o);
                return Task.CompletedTask;
            });
        }

        protected void ProcessCommand<T>(Func<T, Task> func)
        {
            _commandProcessors.Add(typeof(T), o => func((T)o));
        }

        protected void ProcessEvent<T>(Action<T> action)
        {
            _eventProcessors.Add(typeof(T), o => action((T)o));
        }

        #endregion

        protected virtual void OnProcessEvent(object domainEvent)
        {
            Sequence++;
            var eventType = domainEvent.GetType();

            Action<object> processor = null;

            foreach (var kvp in _eventProcessors)
            {
                if (kvp.Key == eventType)
                {
                    processor = kvp.Value;
                    break;
                }
            }

            if (processor == null)
                Log.Warning("Unhandled event with type '{0}'.", eventType.FullName);
            else
                processor(domainEvent);
        }

        class CommandContext
        {
            public IActorRef Sender { get; set; }
            public Guid CommandID { get; set; }
        }

        protected virtual bool CanAcceptStream(string streamId)
        {
            if (String.IsNullOrEmpty(streamId))
                return false;

            return streamId.StartsWith(_category + "-", StringComparison.OrdinalIgnoreCase);
        }

        private void RefuseInvalidStream(AggregateCommandRequest cmd, IActorRef aref)
        {
            aref.Tell(new AggregateCommandRefused {
                CommandID = cmd.CommandID,
                CommandRequest = cmd,
                Reason = "Invalid stream"
            });
        }
    }

    public abstract class Aggregate<TState> : Aggregate
        where TState : new()
    {
        protected TState State { get; } = new TState();
    }
}
