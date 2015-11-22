using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Even
{
    // stores should implement at least IEventStore, and optionally IProjectionStore
    // stores should implement all interfaces in the same type

    public interface IEventStore : IEventStoreWriter, IEventStoreReader, IProjectionStoreWriter, IProjectionStoreReader
    { }

    public interface IEventStoreInitializer
    {
        Task InitializeStore();
    }

    // the following interfaces exist only for internal use

    public interface IEventStoreWriter
    {
        Task WriteAsync(IReadOnlyCollection<IUnpersistedRawStreamEvent> events);
        Task WriteStreamAsync(Stream stream, int expectedSequence, IReadOnlyCollection<IUnpersistedRawEvent> events);
    }

    public interface IEventStoreReader
    {
        Task<long> ReadHighestGlobalSequenceAsync();
        Task ReadAsync(long initialSequence, int count, Action<IPersistedRawEvent> readCallback, CancellationToken ct);
        Task ReadStreamAsync(Stream stream, int initialSequence, int count, Action<IPersistedRawEvent> readCallback, CancellationToken ct);
    }
    
    public interface IProjectionStoreWriter
    {
        Task ClearProjectionIndexAsync(Stream stream);
        Task WriteProjectionIndexAsync(Stream stream, int expectedSequence, IReadOnlyCollection<long> globalSequences);
        Task WriteProjectionCheckpointAsync(Stream stream, long globalSequence);
    }

    public interface IProjectionStoreReader
    {
        Task<long> ReadProjectionCheckpointAsync(Stream stream);
        Task<long> ReadHighestIndexedProjectionGlobalSequenceAsync(Stream stream);
        Task<int> ReadHighestIndexedProjectionStreamSequenceAsync(Stream stream);

        Task ReadIndexedProjectionStreamAsync(Stream stream, int initialSequence, int count, Action<IPersistedRawEvent> readCallback, CancellationToken ct);
    }
}
