using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even
{
    /// <summary>
    /// Represents an event that was persisted to the event store.
    /// </summary>
    public interface IPersistedEvent
    {
        long GlobalSequence { get; }
        Guid EventID { get; }
        Stream Stream { get; }
        string EventType { get; }
        DateTime UtcTimestamp { get; }
        IReadOnlyDictionary<string, object> Metadata { get; }
        object DomainEvent { get; }
    }

    public interface IPersistedEvent<T> : IPersistedEvent
    {
        new T DomainEvent { get; }
    }
}
