using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even
{
    public interface IPersistedEventFactory
    {
        IPersistedEvent CreateEvent(IPersistedRawEvent rawEvent);
        IPersistedStreamEvent CreateStreamEvent(IPersistedRawEvent rawEvent, int streamSequence);
    }

    public class PersistedEventFactory2 : IPersistedEventFactory
    {
        EventRegistry _registry;
        ISerializer _serializer;

        public PersistedEventFactory2(EventRegistry registry, ISerializer serializer)
        {
            Argument.Requires(registry != null);
            Argument.Requires(serializer != null);

            _registry = registry;
            _serializer = serializer;
        }

        public IPersistedEvent CreateEvent(IPersistedRawEvent rawEvent)
        {
            var metadata = _serializer.DeserializeMetadata(rawEvent.Metadata);
            var domainEvent = DeserializeDomainEvent(rawEvent, metadata);

            var t = typeof(PersistedEvent<>).MakeGenericType(domainEvent.GetType());
            return (IPersistedStreamEvent)Activator.CreateInstance(t, rawEvent, metadata, domainEvent);
        }

        public IPersistedStreamEvent CreateStreamEvent(IPersistedRawEvent rawEvent, int streamSequence)
        {
            var metadata = _serializer.DeserializeMetadata(rawEvent.Metadata);
            var domainEvent = DeserializeDomainEvent(rawEvent, metadata);

            var t = typeof(PersistedStreamEvent<>).MakeGenericType(domainEvent.GetType());
            return (IPersistedStreamEvent)Activator.CreateInstance(t, rawEvent, metadata, domainEvent, streamSequence);
        }

        object DeserializeDomainEvent(IPersistedRawEvent rawEvent, IReadOnlyDictionary<string, object> metadata)
        {
            // try loading the type from the registry
            var clrType = _registry.GetClrType(rawEvent.EventType);

            // if it fails, try loading the metadata
            if (clrType == null && metadata != null)
            {
                object clrTypeName;

                if (metadata.TryGetValue(Constants.ClrTypeMetadataKey, out clrTypeName))
                {
                    var s = clrTypeName as string;

                    if (s != null)
                        clrType = Type.GetType(s, false, true);
                }
            }

            // if no type was found, the serializer should use its default type
            var domainEvent = _serializer.DeserializeEvent(rawEvent.Payload, rawEvent.PayloadFormat, clrType);

            return domainEvent;
        }

        class PersistedEvent<T> : IPersistedEvent<T>
        {
            public PersistedEvent(IPersistedRawEvent rawEvent, IReadOnlyDictionary<string, object> metadata, T domainEvent)
            {
                this.GlobalSequence = rawEvent.GlobalSequence;
                this.EventID = rawEvent.EventID;
                this.StreamID = rawEvent.StreamID;
                this.OriginalStreamID = rawEvent.OriginalStreamID;
                this.EventType = rawEvent.EventType;
                this.UtcTimestamp = rawEvent.UtcTimestamp;
                this.Metadata = metadata;
                this.DomainEvent = domainEvent;
            }

            public long GlobalSequence { get; }
            public Guid EventID { get; }
            public string StreamID { get; }
            public string OriginalStreamID { get; }
            public string EventType { get; }
            public DateTime UtcTimestamp { get; }
            public IReadOnlyDictionary<string, object> Metadata { get; }
            public T DomainEvent { get; }

            object IPersistedEvent.DomainEvent => DomainEvent;
        }

        class PersistedStreamEvent<T> : PersistedEvent<T>, IPersistedStreamEvent<T>
        {
            public PersistedStreamEvent(IPersistedRawEvent rawEvent, IReadOnlyDictionary<string, object> metadata, T domainEvent, int streamSequence)
                : base(rawEvent, metadata, domainEvent)
            {
                this.StreamSequence = streamSequence;
            }

            public int StreamSequence { get; }
        }
    }
}
