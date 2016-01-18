using System;
using System.Collections.Generic;

namespace Even
{
    public static class ProjectionEventFactory
    {
        public static IPersistedStreamEvent Create(Stream stream, int streamSequence, IPersistedEvent persistedEvent)
        {
            Argument.RequiresNotNull(stream, nameof(stream));
            Argument.Requires(streamSequence >= 1, nameof(streamSequence));
            Argument.RequiresNotNull(persistedEvent, nameof(persistedEvent));

            var eventType = persistedEvent.DomainEvent.GetType();

            var t = typeof(ProjectionEvent<>).MakeGenericType(eventType);
            var newStream = new Stream(stream, persistedEvent.Stream.OriginalStreamName);
            return (IPersistedStreamEvent)Activator.CreateInstance(t, newStream, streamSequence, persistedEvent);
        }

        class ProjectionEvent<T> : IPersistedStreamEvent<T>
        {
            public ProjectionEvent(Stream stream, int streamSequence, IPersistedEvent<T> persistedEvent)
            {
                Stream = stream;
                StreamSequence = streamSequence;
                _e = persistedEvent;
            }

            public Stream Stream { get; }
            public int StreamSequence { get; }
            IPersistedEvent<T> _e;

            public long GlobalSequence => _e.GlobalSequence;
            public Guid EventID => _e.EventID;
            public string EventType => _e.EventType;
            public DateTime UtcTimestamp => _e.UtcTimestamp;
            public IReadOnlyDictionary<string, object> Metadata => _e.Metadata;
            public T DomainEvent => _e.DomainEvent;

            object IPersistedEvent.DomainEvent => DomainEvent;
        }
    }
}
