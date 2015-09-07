using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;

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

    public interface IPersistedEvent<T> : IPersistedEvent
    {
        new T DomainEvent { get; }
    }

    public static class PersistedEventFactory
    {
        public static IPersistedEvent Create(long globalSequence, string streamId, int streamSequence, UnpersistedEvent unpersistedEvent)
        {
            Contract.Assert(unpersistedEvent != null);

            var t = typeof(EmbeddedPersistedEvent<>).MakeGenericType(unpersistedEvent.DomainEvent.GetType());

            return (IPersistedEvent)Activator.CreateInstance(t, globalSequence, streamId, streamSequence, unpersistedEvent);
        }

        public static IPersistedEvent Create(long globalSequence, Guid eventId, string streamId, int streamSequence, string eventType, DateTime utcTimestamp, IReadOnlyDictionary<string, object> metadata, object domainEvent)
        {
            Contract.Requires(domainEvent != null);

            var t = typeof(LoadedPersistedEvent<>).MakeGenericType(domainEvent.GetType());
            return (IPersistedEvent)Activator.CreateInstance(t, globalSequence, eventId, streamId, streamSequence, eventType, utcTimestamp, metadata, domainEvent);
        }

        public static IPersistedEvent Create(string streamId, int streamSequence, IPersistedEvent @event)
        {
            Contract.Requires(@event != null);
            Contract.Requires(@event.DomainEvent != null);

            var t = typeof(ProjectedPersistedEvent<>).MakeGenericType(@event.DomainEvent.GetType());
            return (IPersistedEvent)Activator.CreateInstance(t, streamId, streamSequence, @event);
        }

        class EmbeddedPersistedEvent<T> : IPersistedEvent<T>
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
            public T DomainEvent => (T) _event.DomainEvent;
            public IReadOnlyDictionary<string, object> Metadata => _event.Metadata;
            public DateTime UtcTimestamp => _event.UtcTimestamp;

            object IPersistedEvent.DomainEvent => _event.DomainEvent;
        }

        class LoadedPersistedEvent<T> : IPersistedEvent<T>
        {
            public LoadedPersistedEvent(long globalSequence, Guid eventId, string streamId, int streamSequence, string eventType, DateTime utcTimestamp, IReadOnlyDictionary<string, object> metadata, T domainEvent)
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
            public T DomainEvent { get; }
            object IPersistedEvent.DomainEvent => DomainEvent;
        }

        class ProjectedPersistedEvent<T> : IPersistedEvent<T>
        {
            public ProjectedPersistedEvent(string streamId, int streamSequence, IPersistedEvent @event)
            {
                StreamID = streamId;
                StreamSequence = streamSequence;
                _event = @event;
            }

            public string StreamID { get; }
            public int StreamSequence { get; }

            private IPersistedEvent _event;

            public long GlobalSequence => _event.GlobalSequence;
            public Guid EventID => _event.EventID;
            public string EventType => _event.EventType;
            public DateTime UtcTimestamp => _event.UtcTimestamp;
            public IReadOnlyDictionary<string, object> Metadata => _event.Metadata;
            public T DomainEvent => (T) _event.DomainEvent;
            object IPersistedEvent.DomainEvent => DomainEvent;
        }
    }
}
