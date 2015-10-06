using Akka.Actor;
using Even.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even
{
    public class StreamEventReader : ReceiveActor
    {
        Props _workerProps;

        public StreamEventReader(IEventStoreReader reader, PersistedEventFactory factory)
        {
            Argument.Requires(reader != null, nameof(reader));
            Argument.Requires(factory != null, nameof(factory));

            _workerProps = Props.Create<ReadStreamWorker>(reader, factory);
            Receive<ReadStreamRequest>(r => HandleRequest(r));
        }

        void HandleRequest(ReadStreamRequest r)
        {
            var worker = Context.ActorOf(_workerProps);
            worker.Forward(r);
        }
    }
}
