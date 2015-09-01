using Akka.Actor;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even.Messages
{
    #region Base Classes

    public abstract class ReplayMessage
    {
        public Guid ReplayID { get; set; }
    }

    public abstract class ReplayRequest : ReplayMessage
    { }

    public abstract class ReplayResponse : ReplayMessage
    { }

    #endregion

    #region Generic Messages

    /// <summary>
    /// Request replay for all events.
    /// </summary>
    public class EventReplayRequest : ReplayMessage
    {
        public int InitialCheckpoint { get; set; }
        public int MaxEvents { get; set; }

        /// <summary>
        /// The query hint may be used or not. If used, ReplayEventGap messages
        /// should be sent to signal the receiver that no events were missed.
        /// </summary>
        public QueryHint QueryHint { get; set; }
    }

    /// <summary>
    /// Represents an event being replayed.
    /// </summary>
    public class ReplayEvent : ReplayResponse
    {
        public IPersistedEvent Event { get; set; }
    }

    /// <summary>
    /// Requests the replay to be cancelled.
    /// Cancellation happens is asynchronously, and the actor may receive messages even after the cancellation has been requested.
    /// </summary>
    public class CancelReplayRequest : ReplayRequest
    { }

    /// <summary>
    /// Signals that the replay request was completed with success.
    /// </summary>
    public class ReplayCompleted : ReplayResponse
    {
        /// <summary>
        /// The last checkpoint the reader saw before completing.
        /// </summary>
        public long LastCheckpoint { get; set; }
    }

    /// <summary>
    /// Signals that the replay was cancelled has happened and no more messages should be sent for the replay.
    /// Further messages for this replay after this message was received should be discarded.
    /// </summary>
    public class ReplayCancelled : ReplayResponse
    { }

    /// <summary>
    /// Signals the replay was aborted for some reason. Compensatory actions will be required.
    /// Further messages for this replay after this message was received should be discarded.
    /// </summary>
    public class ReplayAborted : ReplayResponse
    {
        public string Message { get; set; }
        public Exception Exception { get; set; }
    }

    /// <summary>
    /// Signals the event reader to stop replaying for some reason, and no more messages are required from the stream.
    /// Once received, the event reader should stop sending messages and notify the replay has completed.
    /// Further messages from this replay after this message was sent should be ignored by the sender.
    /// </summary>
    public class ReplayStopRequest : ReplayRequest
    { }

    #endregion

    #region Stream Specific Messages

    /// <summary>
    /// Requests a replay for an aggregate.
    /// </summary>
    public class ReplayAggregateRequest : ReplayRequest
    {
        public string StreamID { get; set; }
        public int InitialSequence { get; set; }
        public bool UseSnapshot { get; set; }
    }

    /// <summary>
    /// A snapshot offer from the event reader.
    /// </summary>
    public class AggregateSnapshotOffer : ReplayResponse
    {
        public IAggregateSnapshot Snapshot { get; set; }
    }

    /// <summary>
    /// Signals that there will be no snapshot offers for the replay.
    /// </summary>
    public class NoAggregateSnapshotOffer : ReplayResponse
    { }

    #endregion

    #region Projection Stream

    public class ProjectionStreamReplayRequest : ReplayRequest
    {
        public string ProjectionID { get; set; }
        public int InitialSequence { get; set; }
        public bool SendIndexedEvents { get; set; }
        public long MaxCheckpoint { get; set; }
        public int MaxEvents { get; internal set; }
    }

    /// <summary>
    /// Signals that no more events will be read from the index, and new messages
    /// will require matching.
    /// </summary>
    public class ProjectionStreamIndexReplayCompleted : ReplayResponse
    {
        public int LastSeenSequence { get; set; }
        public long LastSeenCheckpoint { get; set; }
    }

    public class ProjectionReplayEvent : ReplayResponse
    {
        public IProjectionEvent Event { get; set; }
    }

    public class ProjectionReplayCompleted : ReplayCompleted
    {
        /// <summary>
        /// The last sequence the reader saw when replaying the projection.
        /// </summary>
        public int LastSequence { get; set; }
    }

    #endregion
}
