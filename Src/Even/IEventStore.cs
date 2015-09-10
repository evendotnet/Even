using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Even
{
    // stores should implement at least IStreamStore, and optionally IProjectionStore
    // stores should implement all interfaces in the same type

    public interface IEventStore : IStreamStoreWriter, IProjectionStoreWriter, IStreamStoreReader, IProjectionStoreReader
    { }

    // the following interfaces exist only for internal use

    public interface IStreamStoreWriter
    {
        Task WriteEventsAsync(IReadOnlyCollection<UnpersistedRawEvent> events);
        Task WriteEventsAsync(string streamId, int expectedSequence, IReadOnlyCollection<UnpersistedRawEvent> events);
    }

    public interface IStreamStoreReader
    {
        Task ReadAsync(long initialSequence, int maxEvents, Action<IPersistedRawEvent> readCallback, CancellationToken ct);
        Task ReadStreamAsync(string streamId, int initialSequence, int maxEvents, Action<IPersistedRawEvent> readCallback, CancellationToken ct);
    }
    
    public interface IProjectionStoreWriter
    {
        Task WriteProjectionIndexAsync(string streamId, IReadOnlyCollection<long> globalSequences);
        Task WriteProjectionCheckpointAsync(string streamId, long globalSequence);
    }

    public interface IProjectionStoreReader
    {
        Task<long> ReadProjectionCheckpointAsync(string streamId);
        Task<int> ReadHighestProjectionStreamSequenceAsync(string streamId);

        Task ReadIndexedProjectionStreamAsync(string streamId, int initialSequence, int maxEvents, Action<IPersistedRawEvent> readCallback, CancellationToken ct);
    }
}
