using Akka.Actor;
using Even.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Even
{
    public class EventReader : ReceiveActor
    {
        Props _workerProps;

        public EventReader(IEventStoreReader reader, PersistedEventFactory2 factory)
        {
            Argument.Requires(reader != null, nameof(reader));
            Argument.Requires(factory != null, nameof(factory));

            _workerProps = Props.Create<ReadWorker>(reader, factory);
            Receive<ReadRequest>(r => HandleRequest(r));
        }

        void HandleRequest(ReadRequest r)
        {
            var worker = Context.ActorOf(_workerProps);
            worker.Forward(r);
        }
    }
}
