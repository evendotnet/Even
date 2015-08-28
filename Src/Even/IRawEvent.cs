using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even
{
    public interface IRawEvent
    {
        Guid EventID { get; }
        string StreamID { get; }
        int StreamSequence { get; }
        string EventName { get; }
        IReadOnlyDictionary<string, object> Headers { get; }
        object DomainEvent { get; }
    }
}
