using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Even
{
    // stores should implement at least IStreamStore, and optionally IAggregateSnapshotStore and IProjectionStore
    // stores should implement all interfaces in the same type

    /// <summary>
    /// Allows to read and write events to the store. 
    /// </summary>
    public interface IStreamStore : IStreamStoreWriter, IStreamStoreReader
    { }

    /// <summary>
    /// Allows to read and write snapshots to the store.
    /// </summary>
    public interface IAggregateSnapshotStore : IAggregateSnapshotStoreWriter, IAggregateSnapshotStoreReader
    { }

    /// <summary>
    /// Provides read and write access to the projection index. This store is optional.
    /// </summary>
    public interface IProjectionStore : IProjectionStoreWriter, IProjectionStoreReader
    { }

    public interface IWriteResult
    {
        IReadOnlyCollection<IWrittenEventSequence> Sequences { get; set; }
    }

    public class WriteResult : IWriteResult
    {
        public IReadOnlyCollection<IWrittenEventSequence> Sequences { get; set; }
    }

    public interface IWrittenEventSequence: IEventSequence
    {
        Guid EventID { get; }
    }

    // the following interfaces exist only for internal use

    public interface IStreamStoreWriter
    {
        /// <summary>
        /// Writes the provided events to the stream appending them to the end of each stream.
        /// </summary>
        /// <param name="events">The events to write.</param>
        /// <returns>A task that completes when all events are written and contains the sequences generated for the written events.</returns>
        Task<IWriteResult> WriteEventsAsync(IReadOnlyCollection<IRawStreamEvent> events);

        /// <summary>
        /// Writes the provided events to the specified stream after the expected sequence.
        /// If the stream is not in the expected sequence, a <see cref="UnexpectedSequenceException"/> will be thrown.
        /// </summary>
        /// <param name="streamId">The id of the stream.</param>
        /// <param name="expectedSequence">The expected sequence.</param>
        /// <param name="events">The events to write.</param>
        /// <returns>A task that completes when all events are written and contains the sequences generated for the written events.</returns>
        Task<IWriteResult> WriteEventsStrictAsync(string streamId, int expectedSequence, IReadOnlyCollection<IRawEvent> events);
    }

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
        Task ReadAsync(long initialCheckpoint, int maxEvents, Action<IRawPersistedEvent> readCallback, CancellationToken ct);

        /// <summary>
        /// Reads all events for the specified stream in stream sequence order.
        /// </summary>
        /// <param name="streamId">The ID of the stream.</param>
        /// <param name="initialSequence">The initial stream sequence to read.</param>
        /// <param name="maxEvents">The maximum number of events to read.</param>
        /// <param name="readCallback">A function that will be called for each event as its read.</param>
        /// <param name="ct">A cancellation token to cancel the read from the store.</param>
        /// <returns>A task that completes when all requested events are read.</returns>
        Task ReadStreamAsync(string streamId, int initialSequence, int maxEvents, Action<IRawPersistedEvent> readCallback, CancellationToken ct);
    }
    
    public interface IAggregateSnapshotStoreReader
    {
        /// <summary>
        /// Reads the requested snapshot from the store.
        /// </summary>
        /// <param name="streamId">The id of the aggregate stream.</param>
        /// <returns></returns>
        Task<IRawAggregateSnapshot> ReadAggregateSnapshotAsync(string streamId);
    }

    public interface IAggregateSnapshotStoreWriter
    {
        /// <summary>
        /// Writes the specified snapshot to the store.
        /// </summary>
        /// <param name="streamId">The id of the aggregate stream.</param>
        /// <param name="snapshot">The snapshot to store.</param>
        /// <returns></returns>
        Task WriteAggregateSnapshotAsync(string streamId, IRawAggregateSnapshot snapshot);
    }

    public interface IProjectionStoreWriter
    {
        /// <summary>
        /// Writes the projection indexes to the store.
        /// </summary>
        /// <remarks>
        /// If there is a duplicated sequence error during write, this method must
        /// throw a <see cref="UnexpectedSequenceException"/> so the writer can detect it and correct.
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

    public interface IProjectionStoreReader
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
        Task ReadProjectionEventStreamAsync(string projectionStreamId, int initialSequence, int maxEvents, Action<IRawProjectionEvent> readCallback, CancellationToken ct);
    }
}
