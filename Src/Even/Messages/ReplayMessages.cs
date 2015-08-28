using Akka.Actor;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even.Messages
{
    // base classes

    public abstract class ReplayMessage
    {
        public Guid ReplayID { get; set; }
    }

    // generic messages

    /// <summary>
    /// Represents an event being replayed.
    /// </summary>
    public class ReplayEvent : ReplayMessage
    {
        public IStreamEvent Event { get; set; }
    }

    /// <summary>
    /// Requests the replay to be cancelled.
    /// Cancellation happens is asynchronously, and the actor may receive messages even after the cancellation has been requested.
    /// </summary>
    public class ReplayCancelRequest
    {
        public Guid ReplayID { get; set; }
    }

    /// <summary>
    /// Signals that the replay request was completed with success.
    /// </summary>
    public class ReplayCompleted : ReplayMessage
    { }

    /// <summary>
    /// Signals that the replay was cancelled has happened and no more messages should be sent for the replay.
    /// Further messages for this replay after this message was received should be discarded.
    /// </summary>
    public class ReplayCancelled : ReplayMessage
    { }

    /// <summary>
    /// Signals the replay was aborted for some reason. Compensatory actions will be required.
    /// Further messages for this replay after this message was received should be discarded.
    /// </summary>
    public class ReplayAborted : ReplayMessage
    {
        public Exception Exception { get; set; }
    }

    /// <summary>
    /// Signals the event reader to stop replaying for some reason, and no more messages are required from the stream.
    /// Once received, the event reader should stop sending messages and notify the replay has completed.
    /// Further messages from this replay after this message was sent should be ignored by the sender.
    /// </summary>
    public class ReplayStopRequest : ReplayMessage
    { }

    // aggregate messages

    /// <summary>
    /// Requests a replay for a specific stream.
    /// </summary>
    public class ReplayAggregateRequest : ReplayMessage
    {
        public string StreamID { get; set; }
    }

    /// <summary>
    /// A snapshot offer from the event reader.
    /// </summary>
    public class AggregateReplaySnapshot : ReplayMessage
    {
        public IAggregateSnapshot Snapshot { get; set; }
    }

    // query messages

    /// <summary>
    /// Requests a replay for a specific query.
    /// </summary>
    public class ReplayQueryRequest : ReplayMessage
    {
        public EventStoreQuery Query { get; set; }
        public int InitialCheckpoint { get; set; }
        public int MaxEvents { get; set; } = Int32.MaxValue;
    }

    // projection messages

    public class ReplayProjectionRequest : ReplayMessage
    {
        public EventStoreQuery Query { get; set; }
        public int Sequence { get; set; }
        public int? SequenceHash { get; set; }
        public int? Checkpoint { get; set; }
        public int MaxEvents { get; set; }
    }

    public class ReplayProjectionEvent : ReplayMessage
    {
        public IProjectionStreamEvent ProjectionEvent { get; set; }
    }

    /// <summary>
    /// Signals the projection that the requested state is inconsistent and can't replay from that point.
    /// This may happen because the sequence doesn't exist or the hash of the stream changed.
    /// When the projection receives this message, it should rebuild.
    /// </summary>
    public class InconsistentProjection : ReplayMessage
    {

    }
}
