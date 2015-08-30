using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even
{
    public class RawStreamEvent : IEvent
    {
        public RawStreamEvent(long checkpoint, IRawEvent rawEvent)
        {
            Checkpoint = checkpoint;
            _rawEvent = rawEvent;
            _category = GetCategory();
        }

        private IRawEvent _rawEvent;
        private string _category;

        public long Checkpoint { get; }
        public Guid EventID => _rawEvent.EventID;
        public string StreamID => _rawEvent.StreamID;
        public int StreamSequence => _rawEvent.StreamSequence;
        public string Category { get; }
        public string EventName => _rawEvent.EventName;

        public IReadOnlyDictionary<string, object> Headers => _rawEvent.Headers;
        public object DomainEvent => _rawEvent.DomainEvent;

        private string GetCategory()
        {
            var index = StreamID.IndexOf('-');

            if (index < 0)
                return StreamID;
            else
                return StreamID.Substring(0, index);
        }
    }
}
