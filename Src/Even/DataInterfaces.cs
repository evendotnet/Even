using System;
using System.Collections.Generic;

namespace Even
{
    #region Generic Interfaces

    /// <summary>
    /// Represents the sequence information of an event.
    /// </summary>
    public interface IEventSequence
    {
        long Checkpoint { get; }
        int StreamSequence { get; }
    }

    #endregion

    #region Runtime Interfaces

    /// <summary>
    /// Represents an event.
    /// </summary>
    public interface IEvent
    {
        Guid EventID { get; }
        object DomainEvent { get; }
        DateTime UtcTimeStamp { get; }
    }

    /// <summary>
    /// Represents an event in a stream.
    /// </summary>
    public interface IStreamEvent : IEvent
    {
        string StreamID { get; }
    }

    /// <summary>
    /// Represents a persisted event.
    /// </summary>
    public interface IPersistedEvent : IStreamEvent, IEventSequence
    { }

    /// <summary>
    /// Represents a persisted projection event.
    /// </summary>
    public interface IProjectionEvent : IPersistedEvent
    {
        string ProjectionStreamID { get; }
        int ProjectionSequence { get; }
    }

    public interface IAggregateSnapshot
    {
        string StreamID { get; }
        int StreamSequence { get; }
        object State { get; }
    }

    #endregion

    #region Raw Storage Intefaces

    public interface IRawEvent
    {
        Guid EventID { get; }
        string EventName { get; }
        byte[] Headers { get; }
        byte[] Payload { get; }
        DateTime UtcTimeStamp { get; }
    }

    public interface IRawStreamEvent : IRawEvent
    {
        string StreamID { get; }
    }

    public interface IRawPersistedEvent : IRawStreamEvent, IEventSequence
    { }

    public interface IRawProjectionEvent : IRawPersistedEvent
    {
        string ProjectionStreamID { get; }
        int ProjectionSequence { get; }
    }

    public interface IRawAggregateSnapshot
    {
        string StreamID { get; }
        int StreamSequence { get; }
        string ClrType { get; }
        byte[] Payload { get; }
    }

    #endregion

    #region Projection Index Interfaces

    /// <summary>
    /// Represents an index in the projection index store.
    /// </summary>
    public interface IProjectionStreamIndex
    {
        string ProjectionStreamID { get; }
        int ProjectionSequence { get; }
        long Checkpoint { get; }
    }

    #endregion
}
