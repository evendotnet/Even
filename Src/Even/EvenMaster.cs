using Akka.Actor;
using Even.Messages;
using System;
using System.Collections.Generic;

namespace Even
{
    public class EvenStartInfo
    {
        public EvenStartInfo(IEventStore store, ISerializer serializer, GlobalOptions options)
        {
            Argument.RequiresNotNull(store, nameof(store));
            Argument.RequiresNotNull(serializer, nameof(serializer));
            Argument.RequiresNotNull(options, nameof(options));

            Store = store;
            Serializer = serializer;
            Options = options;
        }

        public IEventStore Store { get; }
        public ISerializer Serializer { get; }
        public GlobalOptions Options { get; }
        public List<StartProjection> Projections { get; } = new List<StartProjection>();
        public List<StartEventProcessor> EventProcessors { get; } = new List<StartEventProcessor>();
        public Dictionary<string, Type> EventTypes { get; } = new Dictionary<string, Type>();
    }

    public class EvenMaster : ReceiveActor
    {
        IActorRef _reader;
        IActorRef _dispatcher;
        IActorRef _writer;
        IActorRef _eventProcessors;
        IActorRef _projectionStreams;
        IActorRef _projections;
        IActorRef _commandProcessors;
        IActorRef _aggregates;

        public static Props CreateProps(EvenStartInfo startInfo)
        {
            Argument.RequiresNotNull(startInfo, nameof(startInfo));

            return Props.Create<EvenMaster>(startInfo);
        }

        public EvenMaster(EvenStartInfo startInfo)
        {
            Initialize(startInfo);

            foreach (var o in startInfo.EventProcessors)
                _eventProcessors.Tell(o);

            foreach (var o in startInfo.Projections)
                _projections.Tell(o);

            Ready();
        }

        void Initialize(EvenStartInfo startInfo)
        {
            var serializer = startInfo.Serializer;
            var store = startInfo.Store;
            var options = startInfo.Options;
            var registry = new EventRegistry();
            var eventFactory = new PersistedEventFactory(registry, serializer);

            foreach (var kvp in startInfo.EventTypes)
                registry.Register(kvp.Key, kvp.Value);

            // initialize reader
            var readerProps = EventStoreReader.CreateProps(store, eventFactory, options);
            _reader = Context.ActorOf(readerProps, "reader");

            // initialize dispatcher
            var dispatcherProps = EventDispatcher.CreateProps(_reader, options);
            _dispatcher = Context.ActorOf(dispatcherProps, "dispatcher");

            // initialize writer
            var writerProps = EventStoreWriter.CreateProps(store, serializer, _dispatcher, options);
            _writer = Context.ActorOf(writerProps, "writer");

            // initialize event processor supervisor
            var eventProcessorsProps = EventProcessorSupervisor.CreateProps(options);
            _eventProcessors = Context.ActorOf(eventProcessorsProps, "eventprocessors");

            // initialize projection streams supervisor
            var projectionStreamsProps = ProjectionStreamSupervisor.CreateProps(_reader, _writer, options);
            _projectionStreams = Context.ActorOf(projectionStreamsProps, "projectionstreams");

            // initialize command processors supervisor
            var commandProcessorsProps = CommandProcessorSupervisor.CreateProps(_writer, options);
            _commandProcessors = Context.ActorOf(commandProcessorsProps, "commandprocessors");

            // initialize aggregates
            var aggregatesProps = AggregateSupervisor.CreateProps(_reader, _writer, options);
            _aggregates = Context.ActorOf(aggregatesProps, "aggregates");
        }

        void Ready()
        {
            Receive<GetEvenServices>(m =>
            {
                Sender.Tell(new EvenServices
                {
                    Reader = _reader,
                    Writer = _writer,
                    Aggregates = _aggregates,
                    CommandProcessors = _commandProcessors,
                    EventProcessors = _eventProcessors,
                    Projections = _projections
                });
            });
        }
    }
}
