using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Even
{
    // stores should implement at least IStreamStore, and optionally IProjectionStore
    // stores should implement all interfaces in the same type

    public interface IStreamStore : IStreamStoreWriter, IStreamStoreReader
    { }

    public interface IProjectionStore : IProjectionStoreWriter, IProjectionStoreReader
    { }

    // the following interfaces exist only for internal use

    public interface IStreamStoreWriter
    {
        Task WriteEventsAsync(IReadOnlyCollection<UnpersistedRawEvent> events);
        Task WriteEventsAsync(string streamId, int expectedSequence, IReadOnlyCollection<UnpersistedRawEvent> events);
    }

    public interface IStreamStoreReader
    {
        Task ReadAsync(long initialCheckpoint, int maxEvents, Action<IPersistedRawEvent> readCallback, CancellationToken ct);
        Task ReadStreamAsync(string streamId, int initialSequence, int maxEvents, Action<IPersistedRawEvent> readCallback, CancellationToken ct);
    }
    
    public interface IProjectionStoreWriter
    {
        Task WriteProjectionIndexAsync(string projectionStreamId, IReadOnlyCollection<IndexSequenceEntry> entries);
        Task WriteProjectionCheckpointAsync(string projectionStreamId, long globalSequence);
    }

    public interface IProjectionStoreReader
    {
        Task<long> ReadProjectionCheckpointAsync(string projectionStreamId);
        Task<int> ReadHighestProjectionStreamSequenceAsync(string projectionStreamId);

        Task ReadIndexedProjectionStreamAsync(string projectionStreamId, int initialSequence, int maxEvents, Action<IPersistedRawEvent> readCallback, CancellationToken ct);
    }
}
