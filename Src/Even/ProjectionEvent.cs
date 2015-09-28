using System;
using System.Collections.Generic;

namespace Even
{
    public static class ProjectionEventFactory
    {
        public static IPersistedStreamEvent Create(string streamId, int streamSequence, IPersistedEvent persistedEvent)
        {
            Argument.RequiresNotNull(streamId, nameof(streamId));
            Argument.Requires(streamSequence >= 1, nameof(streamSequence));
            Argument.RequiresNotNull(persistedEvent, nameof(persistedEvent));

            var eventType = persistedEvent.DomainEvent.GetType();

            var t = typeof(ProjectionEvent<>).MakeGenericType(eventType);
            return (IPersistedStreamEvent)Activator.CreateInstance(t, streamId, streamSequence, persistedEvent);
        }

        class ProjectionEvent<T> : IPersistedStreamEvent<T>
        {
            private ProjectionEvent(string streamId, int streamSequence, IPersistedEvent<T> persistedEvent)
            {
                StreamID = streamId;
                StreamSequence = streamSequence;
                _e = persistedEvent;
            }

            public string StreamID { get; }
            public int StreamSequence { get; }
            IPersistedEvent<T> _e;

            public long GlobalSequence => _e.GlobalSequence;
            public Guid EventID => _e.EventID;
            public string OriginalStreamID => _e.OriginalStreamID;
            public string EventType => _e.EventType;
            public DateTime UtcTimestamp => _e.UtcTimestamp;
            public IReadOnlyDictionary<string, object> Metadata => _e.Metadata;
            public T DomainEvent => _e.DomainEvent;

            object IPersistedEvent.DomainEvent => DomainEvent;
        }
    }
}
