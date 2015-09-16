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
        LinkedList<UnpersistedEvent> _unpersistedEvents = new LinkedList<UnpersistedEvent>();
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

            // because the command processor doesn't care about stream sequences,
            // it delegates the persistence job to a worker and go on to process other commands
            var requests = _unpersistedEvents.Select(u => new PersistenceRequest(u.StreamID, 0, new[] { u })).ToList();

            var workerContext = new WorkerContext
            {
                Command = CurrentCommand,
                Writer = _writer,
                Requests = requests
            };

            var props = PropsFactory.Create<PersistenceWorker>(workerContext);
            var worker = Context.ActorOf(props);

            // send the persistence request immediately on behalf of the worker
            foreach (var request in requests)
                _writer.Tell(requests, worker);

            // clear the events and return
            _unpersistedEvents.Clear();

            return true;
        }

        /// <summary>
        /// Requests persistence of an event to the store.
        /// </summary>
        protected void Persist(string streamId, object domainEvent)
        {
            _unpersistedEvents.AddLast(new UnpersistedEvent(streamId, domainEvent));
        }

        /// <summary>
        /// Handles the persistence results and tell the command sender.
        /// </summary>
        class PersistenceWorker : ReceiveActor
        {
            LinkedList<PersistenceRequest> _requests;

            public PersistenceWorker(WorkerContext ctx)
            {
                _requests = new LinkedList<PersistenceRequest>(ctx.Requests);
                var cmd = ctx.Command;

                Receive<PersistenceSuccess>(msg =>
                {
                    var node = _requests.Nodes().FirstOrDefault(n => n.Value.PersistenceID == msg.PersistenceID);

                    if (node != null)
                        _requests.Remove(node);

                    if (_requests.Count == 0)
                    {
                        cmd.Sender.Tell(new CommandSucceeded
                        {
                            CommandID = cmd.Request.CommandID
                        });

                        Context.Stop(Self);
                    }
                });

                Receive<PersistenceFailure>(msg =>
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
            public IReadOnlyCollection<PersistenceRequest> Requests { get; set; }
        }
    }
}
