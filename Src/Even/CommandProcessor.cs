using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Even.Messages;
using System.Diagnostics.Contracts;
using Akka.Actor;

namespace Even
{
    public class CommandProcessor : ReceiveActor, IWithUnboundedStash
    {
        public IStash Stash { get; set; }

        LinkedList<UnpersistedEvent> _unpersistedEvents = new LinkedList<UnpersistedEvent>();
        IActorRef _writer;
        GlobalOptions _options;
        ObjectHandler _commandHandlers = new ObjectHandler();

        //private IActorRef _supervisor;
        //protected override IActorRef ProcessorSupervisor => _supervisor;

        //// Command processors are cheap to start, so the timeout can be small
        //protected override TimeSpan? IdleTimeout => TimeSpan.FromSeconds(5);

        public CommandProcessor()
        {
            Uninitialized();
        }

        private void Uninitialized()
        {
            Receive<InitializeCommandProcessor>(ini =>
            {
                _writer = ini.Writer;
                _options = ini.Options;

                Become(Ready);
            });
        }

        void Ready()
        {
            SetReceiveTimeout(_options.CommandProcessorIdleTimeout);

            Receive<ProcessorCommand>(async c =>
            {
                // ensure the timeout hasn't expired
                if (c.Timeout.IsExpired)
                {
                    Sender.Tell(new CommandTimeout(c.CommandID));
                    return;
                }

                try
                {
                    await Validate(c.Command);
                    await _commandHandlers.Handle(c.Command);
                }
                // handle command rejections
                catch (RejectException ex)
                {
                    Sender.Tell(new CommandRejected(c.CommandID, ex.Reasons));
                    return;
                }
                // handle unexpected exceptions
                catch (Exception ex)
                {
                    Sender.Tell(new CommandFailed(c.CommandID, ex));
                    return;
                }

                if (_unpersistedEvents.Count > 0)
                {
                    var request = new PersistenceRequest(_unpersistedEvents.ToList());

                    // command processors don't need to wait for persistence, so
                    // we delegate it to a worker and move on to wait for new commands
                    // the worker will wait for responses and notify the sender
                    var props = PersistenceWorker.CreateProps(request.PersistenceID, c.CommandID, Sender, _options.CommandProcessorPersistenceTimeout);
                    var worker = Context.ActorOf(props);

                    // send the request on behalf of the worker and let it wait for responses
                    _writer.Tell(request, worker);

                    // clear the unpersisted events
                    _unpersistedEvents.Clear();
                }
            });

            Receive<ReceiveTimeout>(_ => StopSelf());
        }

        private void Stopping()
        {
            SetReceiveTimeout(_options.CommandProcessorStopTimeout);

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

        protected void StopSelf()
        {
            Context.Parent.Tell(WillStop.Instance);
            Become(Stopping);
        }

        protected virtual async Task Validate(object command)
        {
            var validator = _options.DefaultCommandValidator;

            if (validator != null)
            {
                var reasons = await validator.ValidateAsync(command);

                if (reasons != null)
                    throw new RejectException(reasons);
            }
        }

        class RejectException : Exception
        {
            public RejectException(RejectReasons reasons)
            {
                this.Reasons = reasons;
            }

            public RejectReasons Reasons { get; }
        }

        /// <summary>
        /// Requests persistence of an event to the store.
        /// </summary>
        protected void Persist(string streamName, object domainEvent)
        {
            _unpersistedEvents.AddLast(new UnpersistedEvent(new Stream(streamName), domainEvent));
        }

        /// <summary>
        /// Handles the persistence results and tell the command sender.
        /// </summary>
        class PersistenceWorker : ReceiveActor
        {
            public static Props CreateProps(Guid persistenceId, Guid commandId, IActorRef sender, TimeSpan timeout)
            {
                return Props.Create<PersistenceWorker>(persistenceId, commandId, sender, timeout);
            }

            public PersistenceWorker(Guid persistenceId, Guid commandId, IActorRef sender, TimeSpan timeout)
            {
                SetReceiveTimeout(timeout);

                Receive<PersistenceSuccess>(msg =>
                {
                    sender.Tell(new CommandSucceeded(commandId));
                    Context.Stop(Self);
                });

                Receive<PersistenceFailure>(msg =>
                {
                    sender.Tell(new CommandFailed(commandId, msg.Exception));
                    Context.Stop(Self);
                });

                Receive<ReceiveTimeout>(_ =>
                {
                    sender.Tell(new CommandFailed(commandId, "Timeout waiting for persistence."));
                    Context.Stop(Self);
                });
            }
        }
    }
}
