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
        public EventStoreWriter()
        {
            Receive<InitializeEventStoreWriter>(ini =>
            {
                _writerFactory = ini.WriterFactory;
                _serializer = ini.Serializer;
                _cryptoService = ini.CryptoService;

                Become(ReceivingRequests);
            });
        }

        bool _writeRequested;
        Func<IStorageWriter> _writerFactory;
        ICryptoService _cryptoService;
        IDataSerializer _serializer;
        Queue<BufferEntry> _buffer = new Queue<BufferEntry>();
        ILoggingAdapter Log = Context.GetLogger();

        public void ReceivingRequests()
        {
            Receive<PersistenceRequest>(req =>
            {
                Log.Debug("PersistenceRequest {0} Received", req.EventID);

                // enqueues all writes into a buffer and immediatelly requests writing
                // this allows to handle many writes in batches
                // while still processing quickly single requests

                _buffer.Enqueue(new BufferEntry { Sender = Sender, Request = req });

                if (!_writeRequested)
                {
                    Self.Tell(new WriteBufferCommand());
                    _writeRequested = true;
                }
            });

            Receive<WriteBufferCommand>(o =>
            {
                // the writer handles only one batch at a time
                if (_buffer.Count > 0)
                    AkkaAsyncHelper.Await(WriteBuffer);
            });
        }

        public async Task WriteBuffer()
        {
            // reset the request flag
            _writeRequested = false;

            var writer = _writerFactory();
            var sender = Sender;
            var self = Self;
            var eventStream = Context.System.EventStream;

            // creates a dictionary mapping the events to raw events
            var rawEvents = _buffer.ToDictionary(e => e.Request.EventID, e => new { Sender = e.Sender, RawEvent = CreateRawEvents(e.Request) });

            _buffer.Clear();

            // creates the actual events to be stored
            var storageEvents = rawEvents.Values.Select(o => CreateRawStorageEvent(o.RawEvent));

            await writer.WriteEvents(storageEvents, (e, checkpoint) =>
            {
                Log.Debug("Event {0} Persisted", e.EventID);

                var obj = rawEvents[e.EventID];

                // notify the aggregate
                obj.Sender.Tell(new PersistenceSuccessful { EventID = e.EventID }, self);

                // grab the event from the dictionary and publish the original event (before serialization)
                var rawEvent = new RawStreamEvent(checkpoint, obj.RawEvent);

                // publish the event
                eventStream.Publish(rawEvent);
            });
        }

        private RawEvent CreateRawEvents(PersistenceRequest request)
        {
            var domainEvent = request.DomainEvent;
            var eventType = domainEvent.GetType();
            var eventName = GetEventName(eventType);
            var headers = GetHeaders(eventType);

            return new RawEvent(request.EventID, request.StreamID, request.StreamSequence, eventName, headers, domainEvent);
        }

        private IRawStorageEvent CreateRawStorageEvent(IRawEvent rawEvent)
        {
            var headerBytes = _serializer.Serialize(rawEvent.Headers);
            var payloadBytes = _serializer.Serialize(rawEvent.DomainEvent);

            if (_cryptoService != null)
                payloadBytes = _cryptoService.Encrypt(payloadBytes);

            return new InternalRawStorageEvent(rawEvent, headerBytes, payloadBytes);
        }

        private static string GetEventName(Type type)
        {
            var attr = Attribute.GetCustomAttribute(type, typeof(StreamEventAttribute)) as StreamEventAttribute;
            return attr?.Name ?? type.Name;
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

        class InternalRawStorageEvent : IRawStorageEvent
        {
            public InternalRawStorageEvent(IRawEvent rawEvent, byte[] headers, byte[] payload)
            {
                RawEvent = rawEvent;
                Headers = headers;
                Payload = payload;
            }

            public long Checkpoint { get; }
            public IRawEvent RawEvent { get; }
            public Guid EventID => RawEvent.EventID;
            public string StreamID => RawEvent.StreamID;
            public int StreamSequence => RawEvent.StreamSequence;
            public string EventName => RawEvent.EventName;
            public byte[] Headers { get; }
            public byte[] Payload { get; }
        }

        class WriteBufferCommand { }

        class BufferEntry
        {
            public IActorRef Sender;
            public PersistenceRequest Request;
        }
    }
}
