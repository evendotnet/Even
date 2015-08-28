using Akka.Actor;
using Even.Messages;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even
{
    public class AggregateSupervisor : ReceiveActor
    {
        public AggregateSupervisor()
        {
            Receive<InitializeAggregateSupervisor>(ini => {
                _reader = ini.Reader;
                _writer = ini.Writer;
                Become(ReceivingCommands);
            });
        }

        private IActorRef _reader;
        private IActorRef _writer;
        private Dictionary<string, IActorRef> _aggregates = new Dictionary<string, IActorRef>();

        private void ReceivingCommands()
        {
            Receive<TypedAggregateCommandRequest>(req =>
            {
                var aRef = Context.Child(req.StreamID);

                // if the aggregate doesn't exist, start it
                if (aRef == null || aRef == ActorRefs.Nobody)
                {
                    var props = PropsFactory.Create(req.AggregateType);
                    aRef = Context.ActorOf(props, req.StreamID);
                    aRef.Tell(new InitializeAggregate { StreamID = req.StreamID, Reader = _reader, Writer = _writer });
                }

                aRef.Forward(req);
            });
        }
    }
}
