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

        public EventStoreWriter()
            : this(null, null, null, null)
        { }

        // this constructor is for unit testing only
        public EventStoreWriter(IActorRef serialWriter, IActorRef bufferedWriter, IActorRef indexWriter, IActorRef checkpointWriter)
        {
            Receive<InitializeEventStoreWriter>(ini =>
            {
                try
                {
                    Argument.Requires(ini.StoreWriter != null, "StoreWriter");
                    Argument.Requires(ini.Serializer != null, "Serializer");
                    Argument.Requires(ini.Dispatcher != null, "Dispatcher");

                    var serialProps = PropsFactory.Create<SerialEventStreamWriter>(ini.StoreWriter, ini.Serializer, ini.Dispatcher);
                    _serialWriter = serialWriter ?? Context.ActorOf(serialProps, "serial");

                    var bufferedProps = PropsFactory.Create<BufferedEventWriter>(ini.StoreWriter, ini.Serializer, ini.Dispatcher);
                    _bufferedWriter = bufferedWriter ?? Context.ActorOf(bufferedProps, "buffered");

                    var indexProps = PropsFactory.Create<ProjectionIndexWriter>(ini.StoreWriter, TimeSpan.FromSeconds(2));
                    _indexWriter = indexWriter ?? Context.ActorOf(indexProps, "index");

                    var checkpointProps = PropsFactory.Create<ProjectionCheckpointWriter>(ini.StoreWriter, TimeSpan.FromSeconds(5));
                    _checkpointWriter = checkpointWriter ?? Context.ActorOf(checkpointProps, "checkpoint");

                    Become(Ready);

                    Sender.Tell(InitializationResult.Successful());
                }
                catch (Exception ex)
                {
                    Sender.Tell(InitializationResult.Failed(ex));
                    Context.Stop(Self);
                }
            });
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
