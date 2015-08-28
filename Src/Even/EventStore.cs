using Akka.Actor;
using Akka.Routing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Even.Messages;

namespace Even
{
    public class EventStore : ReceiveActor
    {
        static readonly string ReaderPath = "reader";
        static readonly string WriterPath = "writer";
        static readonly string ProjectionSupervisorPath = "projections";
        static readonly string AggregateSupervisorPath = "aggregates";

        public EventStore(EventStoreSettings settings)
        {
            _settings = settings;

            InitializeChildren();

            Receive<ProjectionSubscriptionRequest>(o => _projections.Forward(o));
        }

        void InitializeChildren()
        {
            // initialize reader
            _reader = Context.ActorOf<EventStoreReader>(ReaderPath);

            _reader.Tell(new InitializeEventStoreReader {
                ReaderFactory = _settings.StorageDriver.CreateReader,
                Serializer = _settings.Serializer,
                CryptoService = _settings.CryptoService
            });

            // initialize writer
            _writer = Context.ActorOf<EventStoreWriter>(WriterPath);

            _writer.Tell(new InitializeEventStoreWriter
            {
                WriterFactory = _settings.StorageDriver.CreateWriter,
                Serializer = _settings.Serializer,
                CryptoService = _settings.CryptoService
            });

            // initialze aggregates
            _aggregates = Context.ActorOf<AggregateSupervisor>(AggregateSupervisorPath);

            _aggregates.Tell(new InitializeAggregateSupervisor
            {
                Reader = _reader,
                Writer = _writer
            });

            // initialize projections

            _projections = Context.ActorOf<ProjectionSupervisor>(ProjectionSupervisorPath);

            _projections.Tell(new InitializeProjectionSupervisor
            {
                Reader = _reader
            });
        }

        EventStoreSettings _settings;
        IActorRef _writer;
        IActorRef _reader;
        IActorRef _projections;
        IActorRef _aggregates;
        
    }
}
