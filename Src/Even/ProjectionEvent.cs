using System;
using System.Collections.Generic;

namespace Even
{

    public interface IProjectionEvent : IPersistedEvent
    {
        string ProjectionStreamID { get; }
        int ProjectionStreamSequence { get; }
    }

    public interface IProjectionEvent<T> : IProjectionEvent
    {
        new T DomainEvent { get; }
    }

    public static class ProjectionEventFactory
    {
        public static IProjectionEvent Create(string projectionStreamId, int projectionStreamSequence, IPersistedEvent persistedEvent)
        {
            var t = typeof(EmbeddedProjectionEvent<>).MakeGenericType(persistedEvent.DomainEvent.GetType());
            return (IProjectionEvent)Activator.CreateInstance(t, projectionStreamId, projectionStreamSequence, persistedEvent);
        }

        class EmbeddedProjectionEvent<T> : IProjectionEvent<T>
        {
            public EmbeddedProjectionEvent(string projectionStreamId, int projectionStreamSequence, IPersistedEvent persistedEvent)
            {
                this.ProjectionStreamID = projectionStreamId;
                this.ProjectionStreamSequence = projectionStreamSequence;
                this._e = persistedEvent;
            }

            public string ProjectionStreamID { get; }
            public int ProjectionStreamSequence { get; }

            IPersistedEvent _e;

            public long GlobalSequence => _e.GlobalSequence;
            public Guid EventID => _e.EventID;
            public string StreamID => _e.StreamID;
            public int StreamSequence => _e.StreamSequence;
            public string EventType => _e.EventType;
            public T DomainEvent => (T) _e.DomainEvent;
            public IReadOnlyDictionary<string, object> Metadata => _e.Metadata;
            public DateTime UtcTimestamp => _e.UtcTimestamp;

            object IPersistedEvent.DomainEvent => _e.DomainEvent;
        }
    }
}
