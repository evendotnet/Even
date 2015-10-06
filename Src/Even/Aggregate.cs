﻿using Akka.Actor;
using Akka.Event;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Threading.Tasks;
using Even.Messages;
using System.Text.RegularExpressions;

namespace Even
{
    public abstract class Aggregate : ReceiveActor, IWithUnboundedStash
    {
        public IStash Stash { get; set; }

        IActorRef _reader;
        IActorRef _writer;
        GlobalOptions _options;

        ObjectHandler _commandHandlers = new ObjectHandler();
        ObjectHandler _eventHandlers = new ObjectHandler();

        LinkedList<UnpersistedEvent> _unpersistedEvents = new LinkedList<UnpersistedEvent>();
        PersistenceRequest _persistenceRequest;

        protected string Category { get; private set; }
        protected string StreamID { get; private set; }
        protected int StreamSequence { get; private set; }
        protected bool IsReplaying { get; private set; }

        CommandContext _currentCommand;

        Guid _replayRequestId;

        public Aggregate()
        {
            Uninitialized();
        }

        #region Actor States

        private void Uninitialized()
        {
            Receive<InitializeAggregate>(ini =>
            {
                Category = ESCategoryAttribute.GetCategory(this.GetType());

                _reader = ini.Reader;
                _writer = ini.Writer;
                _options = ini.Options;

                Become(AwaitingFirstCommand);
            });
        }

        private void AwaitingFirstCommand()
        {
            SetReceiveTimeout(_options.AggregateFirstCommandTimeout);

            Receive<AggregateCommand>(c =>
            {
                // ensure the first command has a valid stream before starting the replay
                if (!IsValidStreamID(c.StreamID))
                {
                    RefuseInvalidStream(c);
                    return;
                }

                // once the first command is received, set the stream id
                StreamID = c.StreamID;

                // and stash the request to process after the replay
                Stash.Stash();

                // then start the replay
                var request = new ReadStreamRequest(c.StreamID, 0, EventCount.Unlimited);
                _reader.Tell(request);
                _replayRequestId = request.RequestID;

                IsReplaying = true;
                Become(Replaying);
            });

            Receive<ReceiveTimeout>(_ => this.StopSelf());
        }
        
        private void Replaying()
        {
            SetReceiveTimeout(_options.ReadRequestTimeout);

            Receive<ReadStreamResponse>(async msg =>
            {
                if (msg.RequestID != _replayRequestId)
                    return;

                Contract.Assert(msg.Event.StreamSequence == StreamSequence + 1);
                await ApplyEventInternal(msg.Event);
            });

            Receive<ReadStreamFinished>(msg =>
            {
                if (msg.RequestID != _replayRequestId)
                    return;

                IsReplaying = false;

                // switch to ready state and start receiving commands
                Become(Ready);

                // unstash the oldest message
                Stash.Unstash();
            });

            // on errors, let the actor restart
            Receive<Cancelled>(msg =>
            {
                if (msg.RequestID != _replayRequestId)
                    return;

                throw new Exception("Replay was cancelled.");
            });

            Receive<Aborted>(msg =>
            {
                if (msg.RequestID != _replayRequestId)
                    return;

                throw new Exception("Replay was aborted.", msg.Exception);
            });

            Receive(new Action<ReceiveTimeout>(_ =>
            {
                throw new Exception("Timeout during replay.");
            }));

            // any other messages are stashed until this step is completed
            ReceiveAny(o => Stash.Stash());
        }

        private void Ready()
        {
            Receive<AggregateCommand>(async ac =>
            {
                // ensure the stream is the same
                if (!String.Equals(ac.StreamID, StreamID, StringComparison.OrdinalIgnoreCase))
                {
                    RefuseInvalidStream(ac);
                    return;
                }

                // ensure the timeout hasn't expire
                if (ac.Timeout.IsExpired)
                {
                    Sender.Tell(new CommandTimeout(ac.CommandID));
                    return;
                }

                // save the context (used for persistence)
                _currentCommand = new CommandContext(Sender, ac);

                try
                {
                    await _commandHandlers.Handle(ac.Command);
                }
                // handle command rejections
                catch (RejectException ex)
                {
                    Sender.Tell(new CommandRejected(ac.CommandID, ex.Reasons));
                    _currentCommand = null;
                    return;
                }
                // handle unexpected exceptions
                catch (Exception ex)
                {
                    Sender.Tell(new CommandFailed(ac.CommandID, ex));
                    _currentCommand = null;
                    return;
                }

                // if there are events to persist, request persistence
                if (_unpersistedEvents.Count > 0)
                {
                    var request = new PersistenceRequest(StreamID, StreamSequence, _unpersistedEvents.ToList());
                    _persistenceRequest = request;
                    _writer.Tell(request);

                    Become(AwaitingPersistence);
                }
                // otherwise
                else
                {
                    OnFinishProcessing();
                }
            });

            OnReady();
        }

        private void AwaitingPersistence()
        {
            SetReceiveTimeout(_options.AggregatePersistenceTimeout);

            Receive<IPersistedEvent>(async e =>
            {
                // locate the unpersisted node
                var node = _unpersistedEvents.Nodes().FirstOrDefault(n => n.Value.EventID == e.EventID);

                if (node != null)
                {
                    _unpersistedEvents.Remove(node);
                    await ApplyEventInternal(e);
                }
            });

            Receive<PersistenceSuccess>(_ =>
            {
                Contract.Assert(_unpersistedEvents.Count == 0, "There should be no unpersisted events left.");
                OnFinishProcessing();
                Become(Ready);
                
            }, msg => msg.PersistenceID == _persistenceRequest.PersistenceID);

            Receive(new Action<UnexpectedStreamSequence>(_ =>
            {
                var command = _currentCommand.Command;
                var nextAttempt = ((command as RetryAggregateCommand)?.Attempt ?? 1) + 1;

                if (nextAttempt <= _options.MaxAggregateProcessAttempts)
                {
                    // forward the current command to itself for retry
                    Self.Tell(new RetryAggregateCommand(command, nextAttempt), _currentCommand.Sender);
                }
                else
                {
                    // fail
                    Sender.Tell(new CommandFailed(command.CommandID, "Too many stream sequence errors."));
                }

                throw new Exception("Unexpected stream sequence");
            }));

            Receive(new Action<PersistenceFailure>(msg =>
            {
                Sender.Tell(new CommandFailed(_currentCommand.Command.CommandID, msg.Exception));

                throw new Exception("Unexpected persistence failure", msg.Exception);
            }));

            Receive(new Action<ReceiveTimeout>(_ => {
                throw new Exception("Timed out awaiting for persistence.");
            }));

            ReceiveAny(_ => Stash.Stash());
        }

        private void Stopping()
        {
            SetReceiveTimeout(_options.AggregateStopTimeout);

            Receive<StopNoticeAcknowledged>(_ =>
            {
                Context.Stop(Self);
            });

            Receive<ReceiveTimeout>(_ =>
            {
                Context.Stop(Self);
            });

            // forward any message to the supervisor so it can forward to the new aggregate
            ReceiveAny(o => Context.Parent.Forward(o));
        }

        #endregion

        #region Command/Event Handler Registration

        protected void OnCommand<T>(Func<T, Task> processor)
        {
            _commandHandlers.AddHandler<T>(o => processor((T)o));
        }

        protected void OnCommand<T>(Action<T> processor)
        {
            _commandHandlers.AddHandler<T>(o => processor((T)o));
        }

        protected void OnEvent<T>(Func<T, Task> processor)
        {
            _eventHandlers.AddHandler<T>(o => processor((T)o));
        }

        protected void OnEvent<T>(Action<T> processor)
        {
            _eventHandlers.AddHandler<T>(o => processor((T)o));
        }

        #endregion

        #region Command Handler Responses

        protected void Reject(string reason)
        {
            Reject(new RejectReasons(reason));
        }

        protected void Reject(RejectReasons reasons)
        {
            Argument.RequiresNotNull(reasons, nameof(reasons));
            throw new RejectException(reasons);
        }

        /// <summary>
        /// Tells the aggregate to persist an event. Once the event is persisted,
        /// it is applied to the aggregate. You may call this command multiple times
        /// for a single command. No new commands are processed until all events
        /// from the same command are persisted.
        /// </summary>
        protected void Persist<T>(T domainEvent, Action<T> persistenceCallback = null)
        {
            Argument.RequiresNotNull(domainEvent, nameof(domainEvent));
            _unpersistedEvents.AddLast(new UnpersistedEvent(StreamID, domainEvent));
        }

        #endregion

        private async Task ApplyEventInternal(IPersistedEvent e)
        {
            StreamSequence++;
            var eventType = e.DomainEvent.GetType();

            await OnReceiveEvent(e);
            await _eventHandlers.Handle(e);
        }

        private void OnFinishProcessing()
        {
            _currentCommand = null;
            Stash.Unstash();
        }

        private void RefuseInvalidStream(AggregateCommand command)
        {
            Sender.Tell(new CommandRefused(command.CommandID, $"The stream '{command.StreamID}' is not valid for this aggregate."));
        }

        protected virtual bool IsValidStreamID(string streamId)
        {
            var pattern = "^" + Category + "-[0-9a-f]{8}-?[0-9a-f]{4}-?[0-9a-f]{4}-?[0-9a-f]{4}-?[0-9a-f]{12}$";
            return Regex.IsMatch(streamId, pattern);
        }

        protected virtual void OnReady()
        { }

        protected void StopSelf()
        {
            Context.Parent.Tell(WillStop.Instance);
            Become(Stopping);
        }

        protected virtual Task OnReceiveEvent(IPersistedEvent e)
        {
            return Task.CompletedTask;
        }

        class CommandContext
        {
            public CommandContext(IActorRef sender, AggregateCommand command)
            {
                this.Sender = sender;
                this.Command = command;
            }

            public IActorRef Sender { get; }
            public AggregateCommand Command { get; }
        }

        class RejectException : Exception
        {
            public RejectException(RejectReasons reasons)
            {
                this.Reasons = reasons;
            }

            public RejectReasons Reasons { get; }
        }
    }

    public abstract class Aggregate<TState> : Aggregate
    {
        protected TState State { get; set; }
    }
}
