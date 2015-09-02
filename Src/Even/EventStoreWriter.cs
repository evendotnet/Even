using Akka.Actor;
using Akka.Event;
using Even.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Even
{
    public class EventStoreWriter : ReceiveActor
    {
        IStreamStoreWriter _writer;
        ICryptoService _cryptoService;
        IDataSerializer _serializer;

        IActorRef _eventWriter;
        IActorRef _snapshotWriter;
        IActorRef _projectionIndexWriter;

        public EventStoreWriter()
        {
            Receive<InitializeEventStoreWriter>(ini =>
            {
                _writer = ini.StoreWriter;
                _serializer = ini.Serializer;
                _cryptoService = ini.CryptoService;

                var ewProps = PropsFactory.Create<EventWriter>(_writer, (Serializer) Serialize);
                _eventWriter = Context.ActorOf(ewProps, "eventwriter");

                // initialize snapshot writer
                var sWriter = _writer as IAggregateSnapshotStoreWriter;

                if (sWriter != null)
                {
                    var swProps = PropsFactory.Create<AggregateSnapshotWriter>(sWriter, (Serializer) Serialize);
                    _snapshotWriter = Context.ActorOf(swProps, "snapshotwriter");
                }

                // initialize projection index writer
                var pWriter = _writer as IProjectionStoreWriter;

                if (pWriter != null)
                {
                    var pwProps = PropsFactory.Create<ProjectionIndexWriter>(pWriter);
                    _projectionIndexWriter = Context.ActorOf(pwProps, "projectionwriter");
                }

                Become(Ready);
            });
        }

        public void Ready()
        {
            Receive<EventPersistenceRequest>(request =>
            {
                _eventWriter.Forward(request);
            });

            Receive<StrictEventPersistenceRequest>(request =>
            {
                _eventWriter.Forward(request);
            });

            // snapshots and indexes don't have persistence responses
            // if the store doesn't support, we just ignore it

            Receive<AggregateSnapshotPersistenceRequest>(request =>
            {
                if (_snapshotWriter != null)
                    _snapshotWriter.Forward(request);
            });

            Receive<ProjectionIndexPersistenceRequest>(request =>
            {
                if (_projectionIndexWriter != null)
                    _projectionIndexWriter.Forward(request);
            });
        }

        public byte[] Serialize(object o, bool encrypt)
        {
            var bytes = _serializer.Serialize(o);

            if (encrypt && _cryptoService != null)
                bytes = _cryptoService.Encrypt(bytes);

            return bytes;
        }

        delegate byte[] Serializer(object input, bool encrypt);

        class EventWriter : ReceiveActor
        {
            IStreamStoreWriter _writer;
            Serializer _serializer;

            public EventWriter(IStreamStoreWriter writer, Serializer serializer)
            {
                _writer = writer;
                _serializer = serializer;

                Receive<EventPersistenceRequest>(async r =>
                {
                    try
                    {
                        await WriteEvents(r);
                        Sender.Tell(new PersistenceSuccessful { PersistenceID = r.PersistenceID });
                    }
                    catch (UnexpectedSequenceException)
                    {
                        Sender.Tell(new UnexpectedSequenceFailure { PersistenceID = r.PersistenceID });
                    }
                    catch (Exception ex)
                    {
                        Sender.Tell(new PersistenceUnknownError { Exception = ex, PersistenceID = r.PersistenceID });
                    }
                });

                Receive<StrictEventPersistenceRequest>(async r =>
                {
                    try
                    {
                        await WriteEventsStrict(r);
                        Sender.Tell(new PersistenceSuccessful { PersistenceID = r.PersistenceID });
                    }
                    catch (UnexpectedSequenceException)
                    {
                        Sender.Tell(new UnexpectedSequenceFailure { PersistenceID = r.PersistenceID });
                    }
                    catch (Exception ex)
                    {
                        Sender.Tell(new PersistenceUnknownError { Exception = ex, PersistenceID = r.PersistenceID });
                    }
                });
            }

            async Task WriteEvents(EventPersistenceRequest request)
            {
                var events = request.Events;

                // serialize the events into raw events
                var rawEvents = events.Select(e => 
                {
                    var type = e.DomainEvent.GetType();

                    var eventName = ESEventAttribute.GetEventName(type);
                    var headers = GetHeaders(type);
                    var headersBytes = _serializer(headers, false);
                    var payloadBytes = _serializer(e.DomainEvent, true);

                    return EventFactory.CreateRawStreamEvent(e, eventName, headersBytes, payloadBytes);
                }).ToList();

                var result = await _writer.WriteEventsAsync(rawEvents);

                // publishes the events in the order they were sent
                foreach (var e in events)
                {
                    var seq = result.Sequences.Single(s => s.EventID == e.EventID);

                    var persistedEvent = EventFactory.CreatePersistedEvent(e, seq.Checkpoint, seq.StreamSequence);

                    // notify the sender
                    Sender.Tell(persistedEvent);

                    // publish to the event stream
                    Context.System.EventStream.Publish(persistedEvent);
                }
            }

            async Task WriteEventsStrict(StrictEventPersistenceRequest request)
            {
                var events = request.Events;

                // serialize the events into raw events
                var rawEvents = events.Select(e =>
                {
                    var type = e.DomainEvent.GetType();

                    var eventName = ESEventAttribute.GetEventName(type);
                    var headers = GetHeaders(type);
                    var headersBytes = _serializer(headers, false);
                    var payloadBytes = _serializer(e.DomainEvent, true);

                    return EventFactory.CreateRawStreamEvent(e, request.StreamID, eventName, headersBytes, payloadBytes);
                }).ToList();

                var result = await _writer.WriteEventsStrictAsync(request.StreamID, request.ExpectedStreamSequence, rawEvents);

                // publishes the events in the order they were sent
                foreach (var e in events)
                {
                    var seq = result.Sequences.Single(s => s.EventID == e.EventID);

                    var persistedEvent = EventFactory.CreatePersistedEvent(e, request.StreamID, seq.Checkpoint, seq.StreamSequence);

                    // notify the sender
                    Sender.Tell(persistedEvent);

                    // publish to the event stream
                    Context.System.EventStream.Publish(persistedEvent);
                }
            }

            private static Dictionary<string, object> GetHeaders(Type type)
            {
                var fullName = type.FullName + "," + type.Assembly.GetName().Name;

                var headers = new Dictionary<string, object>(1)
                {
                    { "CLRType", fullName }
                };

                return headers;
            }
        }

        class AggregateSnapshotWriter  : ReceiveActor
        {
            IAggregateSnapshotStoreWriter _writer;

            public AggregateSnapshotWriter(IAggregateSnapshotStoreWriter writer)
            {
                
            }
        }

        class ProjectionIndexWriter : ReceiveActor
        {
            IProjectionStoreWriter _writer;

            public ProjectionIndexWriter(IProjectionStoreWriter writer)
            {
                _writer = writer;
            }
        }
    }
}
