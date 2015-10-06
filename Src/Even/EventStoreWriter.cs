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
        IActorRef _serialWriter;
        IActorRef _bufferedWriter;
        IActorRef _indexWriter;
        IActorRef _checkpointWriter;

        public static Props CreateProps(IEventStoreWriter storeWriter, ISerializer serializer, IActorRef dispatcher, GlobalOptions options)
        {
            Argument.RequiresNotNull(storeWriter, nameof(storeWriter));
            Argument.RequiresNotNull(serializer, nameof(serializer));
            Argument.RequiresNotNull(dispatcher, nameof(dispatcher));

            return Props.Create<EventStoreWriter>(storeWriter, serializer, dispatcher, options);
        }

        public EventStoreWriter(IEventStoreWriter storeWriter, ISerializer serializer, IActorRef dispatcher, GlobalOptions options)
        {
            var serialProps = Props.Create<SerialEventStreamWriter>(storeWriter, serializer, dispatcher, options);
            _serialWriter = Context.ActorOf(serialProps, "serial");

            var bufferedProps = Props.Create<BufferedEventWriter>(storeWriter, serializer, dispatcher, options);
            _bufferedWriter = Context.ActorOf(bufferedProps, "buffered");

            var indexWriterProps = Props.Create<ProjectionIndexWriter>(storeWriter, options);
            _indexWriter = Context.ActorOf(indexWriterProps, "index");

            var checkpointWriterProps = Props.Create<ProjectionCheckpointWriter>(storeWriter, options);
            _checkpointWriter = Context.ActorOf(checkpointWriterProps, "checkpoint");

            Ready();
        }

        // test only
        public static Props CreateProps(IActorRef serial, IActorRef buffered, IActorRef indexWriter, IActorRef checkpointWriter, GlobalOptions options)
        {
            return Props.Create<EventStoreWriter>(serial, buffered, indexWriter, checkpointWriter, options);
        }

        // test only
        public EventStoreWriter(IActorRef serialWriter, IActorRef bufferedWriter, IActorRef indexWriter, IActorRef checkpointWriter, GlobalOptions options)
        {
            _serialWriter = serialWriter;
            _bufferedWriter = bufferedWriter;
            _indexWriter = indexWriter;
            _checkpointWriter = checkpointWriter;

            Ready();
        }

        public void Ready()
        {
            Receive<PersistenceRequest>(request =>
            {
                if (request.ExpectedStreamSequence == ExpectedSequence.Any)
                    _bufferedWriter.Forward(request);
                else
                    _serialWriter.Forward(request);
            });

            Receive<ProjectionIndexPersistenceRequest>(request =>
            {
                _indexWriter.Forward(request);
            });

            Receive<ProjectionCheckpointPersistenceRequest>(request =>
            {
                _checkpointWriter.Forward(request);
            });
        }
    }
}
