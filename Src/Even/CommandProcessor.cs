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
    public class CommandProcessor : CommandProcessorBase
    {
        LinkedList<IStreamEvent> _unpersistedEvents = new LinkedList<IStreamEvent>();
        IActorRef _writer;

        private IActorRef _supervisor;
        protected override IActorRef ProcessorSupervisor => _supervisor;

        // Command processors are cheap to start, so the timeout can be small
        protected override TimeSpan? IdleTimeout => TimeSpan.FromSeconds(5);

        public CommandProcessor()
        {
            Receive<InitializeCommandProcessor>(ini =>
            {
                _writer = ini.Writer;
                _supervisor = ini.CommandProcessorSupervisor;

                Become(Ready);
            });
        }

        void Ready()
        {
            SetupBase();
        }

        internal override bool HandlePersistenceRequest()
        {
            if (_unpersistedEvents.Count == 0)
                return false;
            
            // because the command processor doesn't care about stream sequence,
            // it delegates the persistence job to a worker and go on to process other commands

            var workerContext = new WorkerContext
            {
                Command = CurrentCommand,
                Writer = _writer,
                Request = new EventPersistenceRequest
                {
                    PersistenceID = Guid.NewGuid(),
                    Events = _unpersistedEvents.ToList()
                }
            };

            var props = PropsFactory.Create<PersistenceWorker>(workerContext);
            var worker = Context.ActorOf(props);

            // send the persistence request on behalf of the worker
            _writer.Tell(workerContext.Request, worker);

            // clear the events and return
            _unpersistedEvents.Clear();

            return true;
        }

        /// <summary>
        /// Requests persistence of an event to the store.
        /// </summary>
        protected void Persist(string streamId, object domainEvent)
        {
            Contract.Requires(streamId != null);
            Contract.Requires(domainEvent != null);
            _unpersistedEvents.AddLast(EventFactory.CreateStreamEvent(streamId, domainEvent));
        }

        /// <summary>
        /// Handles the persistence results and informs the command sender.
        /// </summary>
        class PersistenceWorker : ReceiveActor
        {
            public PersistenceWorker(WorkerContext ctx)
            {
                var persistenceId = ctx.Request.PersistenceID;
                var cmd = ctx.Command;

                Receive<PersistenceSuccessful>(msg =>
                {
                    cmd.Sender.Tell(new CommandSucceeded
                    {
                        CommandID = cmd.Request.CommandID
                    });

                    Context.Stop(Self);

                }, msg => msg.PersistenceID == persistenceId);

                Receive<PersistenceUnknownError>(msg =>
                {
                    cmd.Sender.Tell(new CommandFailed
                    {
                        CommandID = cmd.Request.CommandID,
                        Exception = msg.Exception
                    });

                    Context.Stop(Self);
                });

                //TODO: add some timeout from settings to stop the worker
            }
        }

        /// <summary>
        /// Context information to initialize the worker.
        /// </summary>
        class WorkerContext
        {
            public CommandContext Command { get; set; }
            public IActorRef Writer { get; set; }
            public EventPersistenceRequest Request { get; set; }
        }
    }
}
