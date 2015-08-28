using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even
{
    public class PersistedStreamEvent : IStreamEvent
    {
        public PersistedStreamEvent(long checkpoint, Guid eventId, string streamId, int streamSequence, string eventName, Dictionary<string, object> headers, object domainEvent)
        {
            Checkpoint = checkpoint;
            EventID = eventId;
            StreamID = streamId;
            StreamSequence = streamSequence;
            Headers = headers;
            DomainEvent = domainEvent;
        }

        public long Checkpoint { get; set; }
        public Guid EventID { get; set; }
        public string StreamID { get; set; }
        public int StreamSequence { get; set; }

        public string Category
        {
            get
            {
                var pos = StreamID.IndexOf('-');

                if (pos > 0)
                    return StreamID.Substring(0, pos);

                return null;
            }
        }

        public string EventName { get; }
        public IReadOnlyDictionary<string, object> Headers { get;  }
        public object DomainEvent { get; }
    }
}
