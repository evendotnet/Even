using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even
{
    public class RawEvent : IRawEvent
    {
        public RawEvent(Guid eventID, string streamId, int streamSequence, string eventName, Dictionary<string, object> headers, object domainEvent)
        {
            EventID = eventID;
            StreamID = streamId;
            StreamSequence = streamSequence;
            EventName = eventName;
            Headers = headers;
            DomainEvent = domainEvent;
        }

        public Guid EventID { get; }
        public string StreamID { get; }
        public int StreamSequence { get; }
        public string EventName { get; }
        public IReadOnlyDictionary<string, object> Headers { get; }
        public object DomainEvent { get; }
    }
}
