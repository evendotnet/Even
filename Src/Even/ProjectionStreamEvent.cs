using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even
{
    public class ProjectionEvent : IProjectionEvent
    {
        public ProjectionEvent(string projectionID, int projectionSequence, IEvent @event)
        {
            this.ProjectionID = projectionID;
            this.ProjectionSequence = projectionSequence;
            _event = @event;
        }

        public string ProjectionID { get; }
        public int ProjectionSequence { get; }

        private IEvent _event;

        public long Checkpoint => _event.Checkpoint;
        public Guid EventID => _event.EventID;
        public string StreamID => _event.StreamID;
        public int StreamSequence => _event.StreamSequence;
        public string Category => _event.Category;
        public string EventName => _event.EventName;
        public IReadOnlyDictionary<string, object> Headers => _event.Headers;
        public object DomainEvent => _event.DomainEvent;
    }
}
