using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even
{

    public interface IProjectionEvent : IPersistedEvent
    {
        string ProjectionStreamID { get; }
        int ProjectionStreamSequence { get; }
    }

    public static class ProjectionEventFactory
    {
        public static IProjectionEvent Create(string projectionStreamId, int projectionStreamSequence, IPersistedEvent persistedEvent)
        {
            return new EmbeddedProjectionEvent(projectionStreamId, projectionStreamSequence, persistedEvent);
        }

        class EmbeddedProjectionEvent : IProjectionEvent
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
            public object DomainEvent => _e.DomainEvent;
            public IReadOnlyDictionary<string, object> Metadata => _e.Metadata;
            public DateTime UtcTimestamp => _e.UtcTimestamp;
        }
    }
}
