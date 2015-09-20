using Akka.Actor;
using Akka.Event;
using Even.Messages;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Threading.Tasks;

namespace Even
{
    public class EventStoreWriter : ReceiveActor
    {
        IEventStoreWriter _writer;
        ISerializer _serializer;

        IActorRef _serialWriter;
        IActorRef _bufferedWriter;
        IActorRef _indexWriter;
        IActorRef _checkpointWriter;
        IActorRef _dispatcher;

        public EventStoreWriter()
        {
            Receive<InitializeEventStoreWriter>(ini =>
            {
                _writer = ini.StoreWriter;
                _serializer = ini.Serializer;

                var ewProps = PropsFactory.Create<SerialEventStreamWriter>(_writer, _serializer);
                _serialWriter = Context.ActorOf(ewProps, "eventwriter");

                // initialize projection index writer
                var pWriter = _writer as IProjectionStoreWriter;

                if (pWriter != null)
                {
                    var pwProps = PropsFactory.Create<ProjectionIndexWriter>(pWriter);
                    _indexWriter = Context.ActorOf(pwProps, "projectionwriter");
                }

                Become(Ready);
            });
        }

        public void Ready()
        {
            Receive<PersistenceRequest>(request =>
            {
                _serialWriter.Forward(request);
            });

            Receive<ProjectionIndexPersistenceRequest>(request =>
            {
                if (_indexWriter != null)
                    _indexWriter.Forward(request);
            });
        }
    }
}
