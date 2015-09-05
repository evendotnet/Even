using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even
{
    public interface IPersistedEvent
    {
        long GlobalSequence { get; }
        Guid EventID { get; }
        string StreamID { get; }
        int StreamSequence { get; }
        string EventType { get; }
        DateTime UtcTimestamp { get; }
        IReadOnlyDictionary<string, object> Metadata { get; }
        object DomainEvent { get; }
    }

    public static class PersistedEventFactory
    {
        public static IPersistedEvent Create(long globalSequence, string streamId, int streamSequence, UnpersistedEvent unpersistedEvent)
        {
            return new EmbeddedPersistedEvent(globalSequence, streamId, streamSequence, unpersistedEvent);
        }

        public static IPersistedEvent Create(long globalSequence, Guid eventId, string streamId, int streamSequence, string eventType, DateTime utcTimestamp, IReadOnlyDictionary<string, object> metadata, object domainEvent)
        {
            return new LoadedPersistedEvent(globalSequence, eventId, streamId, streamSequence, eventType, utcTimestamp, metadata, domainEvent);
        }

        class EmbeddedPersistedEvent : IPersistedEvent
        {
            public EmbeddedPersistedEvent(long globalSequence, string streamId, int streamSequence, UnpersistedEvent unpersistedEvent)
            {

                this.GlobalSequence = globalSequence;
                this.StreamID = streamId;
                this.StreamSequence = streamSequence;
                this._event = unpersistedEvent;
            }

            public long GlobalSequence { get; }
            public string StreamID { get; }
            public int StreamSequence { get; }

            UnpersistedEvent _event;

            public Guid EventID => _event.EventID;
            public string EventType => _event.EventType;
            public object DomainEvent => _event.DomainEvent;
            public IReadOnlyDictionary<string, object> Metadata => _event.Metadata;
            public DateTime UtcTimestamp => _event.UtcTimestamp;
        }

        class LoadedPersistedEvent : IPersistedEvent
        {
            public LoadedPersistedEvent(long globalSequence, Guid eventId, string streamId, int streamSequence, string eventType, DateTime utcTimestamp, IReadOnlyDictionary<string, object> metadata, object domainEvent)
            {
                this.GlobalSequence = globalSequence;
                this.EventID = eventId;
                this.StreamID = streamId;
                this.StreamSequence = streamSequence;
                this.EventType = eventType;
                this.UtcTimestamp = utcTimestamp;
                this.Metadata = metadata;
                this.DomainEvent = domainEvent;
            }

            public long GlobalSequence { get; }
            public Guid EventID { get; }
            public string StreamID { get; }
            public int StreamSequence { get; }
            public string EventType { get; }
            public DateTime UtcTimestamp { get; }
            public IReadOnlyDictionary<string, object> Metadata { get; }
            public object DomainEvent { get; }
        }
    }
}
