using Akka.Actor;
using Even.Messages;
using System;
using System.Collections.Generic;

namespace Even
{
    public class AggregateSupervisor : ReceiveActor
    {
        IActorRef _reader;
        IActorRef _writer;
        GlobalOptions _options;

        Dictionary<string, IActorRef> _aggregates = new Dictionary<string, IActorRef>(StringComparer.OrdinalIgnoreCase);

        public static Props CreateProps(IActorRef reader, IActorRef writer, GlobalOptions options)
        {
            Argument.RequiresNotNull(reader, nameof(reader));
            Argument.RequiresNotNull(writer, nameof(writer));
            Argument.RequiresNotNull(options, nameof(options));

            return Props.Create<AggregateSupervisor>(reader, writer, options);
        }

        public AggregateSupervisor(IActorRef reader, IActorRef writer, GlobalOptions options)
        {
            _reader = reader;
            _writer = writer;
            _options = options;

            Ready();
        }

        private void Ready()
        {
            Receive<AggregateCommandEnvelope>(envelope =>
            {
                var command = envelope.Command;
                var key = command.Stream.Name + "|" + envelope.AggregateType.FullName;
                IActorRef aRef;

                // if the aggregate doesn't exist, create it
                if (!_aggregates.TryGetValue(key, out aRef))
                {
                    var props = PropsFactory.Create(envelope.AggregateType);
                    aRef = Context.ActorOf(props);
                    aRef.Tell(new InitializeAggregate(_reader, _writer, _options));
                    _aggregates.Add(key, aRef);
                    Context.Watch(aRef);
                }

                aRef.Forward(command);
            });

            // remove children that are going to stop
            Receive<WillStop>(_ =>
            {
                if (RemoveActiveProcessor(Sender))
                    Sender.Tell(StopNoticeAcknowledged.Instance);
            });

            // remove terminated children
            Receive<Terminated>(t => RemoveActiveProcessor(t.ActorRef));
        }

        bool RemoveActiveProcessor(IActorRef aRef)
        {
            foreach (var kvp in _aggregates)
            {
                if (aRef.Equals(kvp.Value))
                {
                    _aggregates.Remove(kvp.Key);
                    return true;
                }
            }

            return false;
        }

        protected override SupervisorStrategy SupervisorStrategy()
        {
            // default behavior to handle exceptions in aggregates is to always restart
            return new OneForOneStrategy(ex => Directive.Restart);
        }
    }
}
