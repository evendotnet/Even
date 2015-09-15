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

        IActorRef _eventWriter;
        IActorRef _indexWriter;

        public EventStoreWriter()
        {
            Receive<InitializeEventStoreWriter>(ini =>
            {
                _writer = ini.StoreWriter;
                _serializer = ini.Serializer;

                var ewProps = PropsFactory.Create<SerialEventStreamWriter>(_writer, _serializer);
                _eventWriter = Context.ActorOf(ewProps, "eventwriter");

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
                _eventWriter.Forward(request);
            });

            Receive<ProjectionIndexPersistenceRequest>(request =>
            {
                if (_indexWriter != null)
                    _indexWriter.Forward(request);
            });
        }
    }

    public abstract class BaseEventWriter : ReceiveActor
    {
        IEventStoreWriter _writer;
        ISerializer _serializer;

        public BaseEventWriter(IEventStoreWriter writer, ISerializer serializer)
        {
            Contract.Requires(writer != null);
            Contract.Requires(serializer != null);

            this._writer = writer;
            this._serializer = serializer;
        }

        protected async Task WriteEvents(string streamId, int expectedStreamSequence, IReadOnlyCollection<UnpersistedEvent> events)
        {
            // serialize the events into raw events
            var rawEvents = events.Select(e =>
            {
                var format = EvenStorageFormatAttribute.GetStorageFormat(e.DomainEvent.GetType());
                var metadata = _serializer.SerializeMetadata(e.Metadata);
                var payload = _serializer.SerializeEvent(e.DomainEvent, format);

                var re = new UnpersistedRawStreamEvent(e.EventID, streamId, e.EventType, e.UtcTimestamp, metadata, payload, format);

                return re;
            }).ToList();

            // writes all events to the store
            await _writer.WriteStreamAsync(streamId, expectedStreamSequence, rawEvents);

            // ensure the sequences were set
            Contract.Assert(rawEvents.All(e => e.SequenceWasSet), "Some or all sequences were not set after write.");

            // publishes the events in the order they were sent
            var i = 0;
            var sequence = expectedStreamSequence + 1;

            foreach (var e in events)
            {
                var rawEvent = rawEvents[i++];
                var persistedEvent = PersistedEventFactory.Create(rawEvent.GlobalSequence, streamId, sequence++, e);

                // notify the sender
                Sender.Tell(persistedEvent);

                // publish to the event stream
                Context.System.EventStream.Publish(persistedEvent);
            }
        }
    }

    public class SerialEventStreamWriter : BaseEventWriter
    {
        public SerialEventStreamWriter(IEventStoreWriter writer, ISerializer serializer)
            : base(writer, serializer)
        {
            Receive<PersistenceRequest>(async request =>
            {
                try
                {
                    await WriteEvents(request.StreamID, request.ExpectedStreamSequence, request.Events);
                    Sender.Tell(new PersistenceSuccess(request.PersistenceID));
                }
                catch (UnexpectedStreamSequenceException)
                {
                    Sender.Tell(new UnexpectedStreamSequence(request.PersistenceID));
                }
                catch (DuplicatedEventException)
                {
                    Sender.Tell(new DuplicatedEvent(request.PersistenceID));
                }
                catch (Exception ex)
                {
                    Sender.Tell(new PersistenceFailure(request.PersistenceID, "Unpexpected error", ex));
                }
            });
        }
    }

    public class ProjectionIndexWriter : ReceiveActor
    {
        IProjectionStoreWriter _writer;
        LinkedList<ProjectionIndexPersistenceRequest> _buffer = new LinkedList<ProjectionIndexPersistenceRequest>();
        bool _flushRequested;

        TimeSpan _flushDelay = TimeSpan.FromSeconds(5);

        public ProjectionIndexWriter(IProjectionStoreWriter writer)
        {
            _writer = writer;

            Receive<ProjectionIndexPersistenceRequest>(request => AddToBuffer(request));
            Receive<WriteBufferCommand>(_ => WriteBuffer());
        }

        void AddToBuffer(ProjectionIndexPersistenceRequest request)
        {
            _buffer.AddLast(request);

            if (_buffer.Count > 500)
                Self.Tell(new WriteBufferCommand());

            else if (!_flushRequested)
            {
                _flushRequested = true;
                Context.System.Scheduler.ScheduleTellOnce(_flushDelay, Self, new WriteBufferCommand(), Self);
            }
        }

        async Task WriteBuffer()
        {
            _flushRequested = false;

            var re = from e in _buffer
                     group e by e.ProjectionStreamID into g
                     select new
                     {
                         ProjectionStreamID = g.Key,
                         Entries = g.Select(o => o.GlobalSequence).ToList()
                     };
            // TODO: take into account the projection sequence
            //foreach (var o in re)
            //    await _writer.WriteProjectionIndexAsync(o.ProjectionStreamID, o.Entries);

            _buffer.Clear();
        }

        class WriteBufferCommand { }
    }
}
