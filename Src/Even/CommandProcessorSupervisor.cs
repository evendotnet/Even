using Akka.Actor;
using Akka.Event;
using Even.Messages;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even
{
    public class CommandProcessorSupervisor : ReceiveActor
    {
        private IActorRef _reader;
        private IActorRef _writer;
        private Dictionary<string, IActorRef> _activeProcessors = new Dictionary<string, IActorRef>();

        public CommandProcessorSupervisor()
        {
            Receive<InitializeCommandProcessorSupervisor>(ini => {
                _reader = ini.Reader;
                _writer = ini.Writer;
                Become(ReceivingCommands);
            });
        }

        protected override SupervisorStrategy SupervisorStrategy()
        {
            // default behavior to handle exceptions in aggregates and processors is to always restart
            return new OneForOneStrategy(ex => Directive.Restart);
        }

        private void ReceivingCommands()
        {
            Receive<TypedCommandRequest>(async request =>
            {
                IActorRef aRef;

                // if the processor doesn't exist, create it
                if (!_activeProcessors.TryGetValue(request.StreamID, out aRef))
                {
                    var props = PropsFactory.Create(request.AggregateType);
                    aRef = Context.ActorOf(props);

                    var state = await aRef.Ask(new InitializeAggregate
                    {
                        CommandProcessorSupervisor = Self,
                        StreamID = request.StreamID,
                        Reader = _reader,
                        Writer = _writer,
                    }) as AggregateInitializationState;

                    // if the initialization failed, tell the sender
                    if (state == null || !state.Initialized)
                    {
                        Sender.Tell(new CommandRefused
                        {
                            CommandID = request.CommandID,
                            Reason = "Processor Initializing Error: " + state?.InitializationFailureReason ?? "Unknown"
                        });

                        Context.Stop(aRef);

                        return;
                    }

                    // if we get here, the processor initialized fine
                    _activeProcessors.Add(request.StreamID, aRef);
                    Context.Watch(aRef);
                }

                aRef.Forward(request);
            });

            // remove children that are going to stop or terminated
            Receive<WillStop>(_ => RemoveActiveProcessor(Sender));
            Receive<Terminated>(t => RemoveActiveProcessor(t.ActorRef));
        }

        void RemoveActiveProcessor(IActorRef aRef)
        {
            foreach (var kvp in _activeProcessors)
            {
                if (aRef.Equals(kvp.Value))
                {
                    Context.GetLogger().Debug("Processor {0} will stop", aRef);

                    _activeProcessors.Remove(kvp.Key);
                    return;
                }
            }
        }
    }
}
