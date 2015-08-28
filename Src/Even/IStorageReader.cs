using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Even
{
    public interface IStorageReader
    {
        Task<long> GetHighestCheckpointAsync();
        Task<int> GetHighestStreamSequenceAsync(string streamId);
        //Task<Dictionary<string, int>> GetHighestStreamSequence(string[] streamIds);

        Task<IRawAggregateSnapshot> ReadAggregateSnapshotAsync(string streamId, CancellationToken ct);
        Task ReadAggregateStreamAsync(string streamId, int initialSequence, int maxEvents, Action<IRawStorageEvent> readCallback, CancellationToken ct);

        Task QueryCheckpointsAsync(EventStoreQuery esQuery, long initialCheckpoint, int maxEvents, Func<long, bool> readCallback, CancellationToken ct);
        Task QueryEventsAsync(EventStoreQuery esQuery, long initialCheckpoint, int maxEvents, Func<IRawStorageEvent, bool> readCallback, CancellationToken ct);
    }
}