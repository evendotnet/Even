using System;
using System.Collections.Generic;

namespace Even
{
    public interface IEvent
    {
        long Checkpoint { get; }
        Guid EventID { get; }
        string StreamID { get; }
        int StreamSequence { get; }
        string Category { get; }
        string EventName { get; }

        IReadOnlyDictionary<string, object> Headers { get; }
        object DomainEvent { get; }
    }
}
