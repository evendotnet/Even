using Akka.Actor;
using Even.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even
{
    public class IndexedProjectionStreamReader : ReceiveActor
    {
        Props _workerProps;

        public IndexedProjectionStreamReader(IEventStoreReader reader, PersistedEventFactory2 factory)
        {
            Argument.Requires(reader != null, nameof(reader));
            Argument.Requires(factory != null, nameof(factory));

            _workerProps = Props.Create<ReadIndexedProjectionStreamWorker>(reader, factory);
            Receive<ReadIndexedProjectionStreamRequest>(r => HandleRequest(r));
        }

        void HandleRequest(ReadIndexedProjectionStreamRequest r)
        {
            var worker = Context.ActorOf(_workerProps);
            worker.Forward(r);
        }
    }
}
