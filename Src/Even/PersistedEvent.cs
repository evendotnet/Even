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
        string OriginalStreamID { get; }
        string EventType { get; }
        DateTime UtcTimestamp { get; }
        IReadOnlyDictionary<string, object> Metadata { get; }
        object DomainEvent { get; }
    }

    public interface IPersistedEvent<T> : IPersistedEvent
    {
        new T DomainEvent { get; }
    }

    public interface IPersistedStreamEvent : IPersistedEvent
    {
        int StreamSequence { get; }
    }

    public interface IPersistedStreamEvent<T> : IPersistedStreamEvent, IPersistedEvent<T>
    { }

    public static class PersistedEventFactory
    {
        public static IPersistedEvent Create(long globalSequence, UnpersistedEvent unpersistedEvent)
        {
            Contract.Assert(unpersistedEvent != null);

            var t = typeof(EmbeddedPersistedEvent<>).MakeGenericType(unpersistedEvent.DomainEvent.GetType());

            return (IPersistedEvent)Activator.CreateInstance(t, globalSequence, -1, unpersistedEvent);
        }

        public static IPersistedStreamEvent Create(long globalSequence, int streamSequence, UnpersistedEvent unpersistedEvent)
        {
            Contract.Assert(unpersistedEvent != null);

            var t = typeof(EmbeddedPersistedEvent<>).MakeGenericType(unpersistedEvent.DomainEvent.GetType());

            return (IPersistedStreamEvent)Activator.CreateInstance(t, globalSequence, streamSequence, unpersistedEvent);
        }

        public static IPersistedStreamEvent Create(long globalSequence, Guid eventId, string streamId, int streamSequence, string eventType, DateTime utcTimestamp, IReadOnlyDictionary<string, object> metadata, object domainEvent)
        {
            Contract.Requires(domainEvent != null);

            var t = typeof(LoadedPersistedEvent<>).MakeGenericType(domainEvent.GetType());
            return (IPersistedStreamEvent)Activator.CreateInstance(t, globalSequence, eventId, streamId, streamSequence, eventType, utcTimestamp, metadata, domainEvent);
        }

        public static IPersistedStreamEvent Create(string streamId, int streamSequence, IPersistedEvent @event)
        {
            Contract.Requires(@event != null);
            Contract.Requires(@event.DomainEvent != null);

            var t = typeof(ProjectedPersistedEvent<>).MakeGenericType(@event.DomainEvent.GetType());
            return (IPersistedStreamEvent)Activator.CreateInstance(t, streamId, streamSequence, @event);
        }

        class EmbeddedPersistedEvent<T> : IPersistedStreamEvent<T>
        {
            public EmbeddedPersistedEvent(long globalSequence, int streamSequence, UnpersistedEvent unpersistedEvent)
            {
                this.GlobalSequence = globalSequence;
                this.StreamSequence = streamSequence;
                this._event = unpersistedEvent;
            }

            public long GlobalSequence { get; }
            public int StreamSequence { get; }

            UnpersistedEvent _event;

            public Guid EventID => _event.EventID;
            public string StreamID => _event.StreamID;
            public string OriginalStreamID => _event.StreamID;
            public string EventType => _event.EventType;
            public T DomainEvent => (T) _event.DomainEvent;
            public IReadOnlyDictionary<string, object> Metadata => _event.Metadata;
            public DateTime UtcTimestamp => _event.UtcTimestamp;

            object IPersistedEvent.DomainEvent => _event.DomainEvent;

        }

        class LoadedPersistedEvent<T> : IPersistedStreamEvent<T>
        {
            public LoadedPersistedEvent(long globalSequence, Guid eventId, string streamId, string originalStreamId, int streamSequence, string eventType, DateTime utcTimestamp, IReadOnlyDictionary<string, object> metadata, T domainEvent)
            {
                this.GlobalSequence = globalSequence;
                this.EventID = eventId;
                this.StreamID = streamId;
                this.OriginalStreamID = originalStreamId;
                this.StreamSequence = streamSequence;
                this.EventType = eventType;
                this.UtcTimestamp = utcTimestamp;
                this.Metadata = metadata;
                this.DomainEvent = domainEvent;
            }

            public long GlobalSequence { get; }
            public Guid EventID { get; }
            public string StreamID { get; }
            public string OriginalStreamID { get; }
            public int StreamSequence { get; }
            public string EventType { get; }
            public DateTime UtcTimestamp { get; }
            public IReadOnlyDictionary<string, object> Metadata { get; }
            public T DomainEvent { get; }
            object IPersistedEvent.DomainEvent => DomainEvent;
        }

        class ProjectedPersistedEvent<T> : IPersistedStreamEvent<T>
        {
            public ProjectedPersistedEvent(string originalStreamId, string streamId, int streamSequence, IPersistedEvent @event)
            {
                OriginalStreamID = originalStreamId;
                StreamID = streamId;
                StreamSequence = streamSequence;
                _event = @event;
            }

            public string StreamID { get; }
            public string OriginalStreamID { get; }
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
