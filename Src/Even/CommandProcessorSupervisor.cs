using Akka.Actor;
using Even.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even
{
    public class CommandProcessorSupervisor : ReceiveActor
    {
        IActorRef _writer;
        GlobalOptions _options;

        Dictionary<Type, IActorRef> _processors = new Dictionary<Type, IActorRef>();

        public static Props CreateProps(IActorRef writer, GlobalOptions options)
        {
            Argument.RequiresNotNull(writer, nameof(writer));
            Argument.RequiresNotNull(options, nameof(options));

            return Props.Create<CommandProcessorSupervisor>(writer, options);
        }

        public CommandProcessorSupervisor(IActorRef writer, GlobalOptions options)
        {
            this._writer = writer;
            this._options = options;

            Ready();
        }

        void Ready()
        {
            Receive<ProcessorCommandEnvelope>(envelope =>
            {
                var command = envelope.Command;
                var key = envelope.ProcessorType;

                IActorRef aRef;

                // if the aggregate doesn't exist, create it
                if (!_processors.TryGetValue(key, out aRef))
                {
                    var props = PropsFactory.Create(envelope.ProcessorType);
                    aRef = Context.ActorOf(props);
                    aRef.Tell(new InitializeCommandProcessor(_writer, _options));
                    _processors.Add(key, aRef);
                    Context.Watch(aRef);
                }

                aRef.Forward(command);
            });

            // remove children that are going to stop
            Receive<WillStop>(_ =>
            {
                RemoveActiveProcessor(Sender);
                Sender.Tell(StopNoticeAcknowledged.Instance);
            });

            // remove terminated children
            Receive<Terminated>(t => RemoveActiveProcessor(t.ActorRef));
        }

        void RemoveActiveProcessor(IActorRef aRef)
        {
            foreach (var kvp in _processors)
            {
                if (aRef.Equals(kvp.Value))
                {
                    _processors.Remove(kvp.Key);
                    return;
                }
            }
        }
    }
}
