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
        internal static readonly string ReaderPath = "reader";
        internal static readonly string WriterPath = "writer";
        internal static readonly string StreamsPath = "streams";
        internal static readonly string ProjectionSupervisorPath = "projections";
        internal static readonly string AggregateSupervisorPath = "aggregates";
        internal static readonly string IndexWriterPath = "indexwriter";

        public EventStore(EventStoreSettings settings)
        {
            _settings = settings;

            Receive<InitializeEventStore>(ini =>
            {
                InitializeChildren();

                foreach (var p in ini.Projections)
                {
                    _projections.Tell(new StartProjection
                    {
                        ProjectionType = p,
                        ProjectionID = p.Name
                    });
                }
            });

            Receive<ProjectionSubscriptionRequest>(o => _streams.Forward(o));
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

            // initialize index writer
            _indexWriter = Context.ActorOf<ProjectionIndexWriter>(IndexWriterPath);

            _indexWriter.Tell(new InitializeProjectionIndexWriter
            {
                WriterFactory = _settings.StorageDriver.CreateWriter
            });

            // initialize projection streams
            _streams = Context.ActorOf<ProjectionStreams>(StreamsPath);

            _streams.Tell(new InitializeProjectionStreams
            {
                Reader = _reader,
                IndexWriter = _indexWriter
            });

            // initialize projections supervisor
            _projections = Context.ActorOf<ProjectionSupervisor>(ProjectionSupervisorPath);

            _projections.Tell(new InitializeProjectionSupervisor
            {
                Streams = _streams
            });

            // initialze aggregates
            _aggregates = Context.ActorOf<AggregateSupervisor>(AggregateSupervisorPath);

            _aggregates.Tell(new InitializeAggregateSupervisor
            {
                Reader = _reader,
                Writer = _writer
            });

        }

        EventStoreSettings _settings;
        IActorRef _writer;
        IActorRef _reader;
        IActorRef _streams;
        IActorRef _projections;
        IActorRef _aggregates;
        private IActorRef _indexWriter;
    }
}
