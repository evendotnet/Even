using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Even
{
    /// <summary>
    /// Allows to read and write events to the store. 
    /// </summary>
    public interface IStreamStore : IStreamStoreWriter, IStreamStoreReader
    { }

    /// <summary>
    /// Allows to write events to the store.
    /// </summary>
    public interface IStreamStoreWriter
    {
        /// <summary>
        /// Writes the provided events to the store.
        /// </summary>
        /// <remarks>
        /// If there is a duplicated sequence error during write, this method must
        /// throw a <see cref="StorageSequenceException"/> so the writer can detect it and correct.
        /// </remarks>
        /// <param name="events">The events to write.</param>
        /// <param name="writtenCallback">A function that will be called with the event and the checkpoint after each event is written.</param>
        /// <returns>A task that completes when all provided events are written.</returns>
        Task WriteEventsAsync(IEnumerable<IRawStorageEvent> events, Action<IRawStorageEvent, long> writtenCallback);
    }

    /// <summary>
    /// Allows to read events from the store.
    /// </summary>
    public interface IStreamStoreReader
    {
        /// <summary>
        /// Reads the highest checkpoint from the store.
        /// </summary>
        Task<long> ReadHighestCheckpointAsync();

        /// <summary>
        /// Reads the highest stream sequence for a specified stream.
        /// </summary>
        Task<int> ReadHighestStreamSequenceAsync(string streamId);

        /// <summary>
        /// Reads all events from the store in checkpoint order.
        /// </summary>
        /// <param name="initialCheckpoint">The first checkpoint to read.</param>
        /// <param name="maxEvents">The maximum number of events to read.</param>
        /// <param name="readCallback">A function that will be called for each event as its read.</param>
        /// <param name="ct">A cancellation token to cancel the read from the store.</param>
        /// <returns>A task that completes when all requested events are read.</returns>
        Task ReadAsync(long initialCheckpoint, int maxEvents, Action<IRawStorageEvent> readCallback, CancellationToken ct);

        /// <summary>
        /// Reads all events for the specified stream in stream sequence order.
        /// </summary>
        /// <param name="streamId">The ID of the stream.</param>
        /// <param name="initialSequence">The initial stream sequence to read.</param>
        /// <param name="maxEvents">The maximum number of events to read.</param>
        /// <param name="readCallback">A function that will be called for each event as its read.</param>
        /// <param name="ct">A cancellation token to cancel the read from the store.</param>
        /// <returns>A task that completes when all requested events are read.</returns>
        Task ReadStreamAsync(string streamId, int initialSequence, int maxEvents, Action<IRawStorageEvent> readCallback, CancellationToken ct);
    }
    
    /// <summary>
    /// Allows to read and write snapshots to the store.
    /// </summary>
    public interface IAggregateSnapshotStore
    {
        /// <summary>
        /// Writes the specified snapshot to the store.
        /// </summary>
        /// <param name="streamId">The id of the aggregate stream.</param>
        /// <param name="snapshot">The snapshot to store.</param>
        /// <returns></returns>
        Task WriteStreamSnapshotAsync(string streamId, IRawAggregateSnapshot snapshot);

        /// <summary>
        /// Reads the requested snapshot from the store.
        /// </summary>
        /// <param name="streamId">The id of the aggregate stream.</param>
        /// <returns></returns>
        Task<IRawAggregateSnapshot> ReadStreamSnapshotAsync(string streamId);
    }

    /// <summary>
    /// Provides read and write access to the projection index. This store is optional.
    /// </summary>
    public interface IProjectionStore
    {
        /// <summary>
        /// Read the highest checkpoint for the specified projection stream from the store.
        /// </summary>
        /// <param name="projectionStreamId">The projection stream id.</param>
        /// <returns>The checkpoint value.</returns>
        Task<long> ReadHighestProjectionCheckpointAsync(string projectionStreamId);

        /// <summary>
        /// Reads the highest sequence for the specified projection stream from the store.
        /// </summary>
        /// <param name="projectionStreamId">The projection stream id.</param>
        /// <returns>The projection stream sequence value.</returns>
        Task<int> ReadHighestProjectionStreamSequenceAsync(string projectionStreamId);

        /// <summary>
        /// Reads all events for the specified projection stream in stream sequence order.
        /// </summary>
        /// <param name="projectionStreamId">The id of the projection stream.</param>
        /// <param name="initialSequence">The first stream sequence to read.</param>
        /// <param name="maxEvents">The maximum number of events to read.</param>
        /// <param name="readCallback">A function that will be called for each event as its read.</param>
        /// <param name="ct">A cancellation token to cancel the read from the store.</param>
        /// <returns>A task that completes when all requested events are read.</returns>
        Task ReadProjectionEventStreamAsync(string projectionStreamId, int initialSequence, int maxEvents, Action<IRawStorageProjectionEvent> readCallback, CancellationToken ct);

        /// <summary>
        /// Writes the projection indexes to the store.
        /// </summary>
        /// <remarks>
        /// If there is a duplicated sequence error during write, this method must
        /// throw a <see cref="StorageSequenceException"/> so the writer can detect it and correct.
        /// </remarks>
        /// <param name="entries">The index entries to store.</param>
        /// <returns>A task that completes when all events are written to the store.</returns>
        Task WriteProjectionIndexAsync(IEnumerable<IProjectionStreamIndex> entries);

        /// <summary>
        /// Stores the maximum checkpoint the stream has seen.
        /// </summary>
        /// <param name="projectionId">The id of the projection stream.</param>
        /// <param name="checkpoint">The checkpoint to write.</param>
        /// <returns>A task that completes when the checkpoint is written.</returns>
        Task WriteProjectionCheckpointAsync(string projectionId, long checkpoint);
    }
}
