using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Even
{
    public interface IStorageReader
    {
        // global
        Task<long> GetHighestCheckpointAsync();

        // streams
        Task<int> GetHighestStreamSequenceAsync(string streamId);
        Task<IRawAggregateSnapshot> ReadStreamSnapshotAsync(string streamId, CancellationToken ct);
        Task ReadStreamAsync(string streamId, int initialSequence, int maxEvents, Action<IRawStorageEvent> readCallback, CancellationToken ct);
        Task ReadAsync(long initialCheckpoint, int maxEvents, Action<IRawStorageEvent> readCallback, CancellationToken ct);

        // projections
        Task<long> GetHighestProjectionCheckpoint(string projectionStreamId);
        Task<int> GetHighestProjectionStreamSequenceAsync(string projectionStreamId);
        Task ReadProjectionEventStreamAsync(string projectionStreamId, int initialSequence, int maxEvents, Action<IRawStorageProjectionEvent> readCallback, CancellationToken ct);
    }
}