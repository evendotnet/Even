//using Akka.Actor;
//using System;
//using System.Collections.Generic;
//using System.Diagnostics.Contracts;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace Even.Messages
//{
//    #region Base Classes
//    [Obsolete]
//    public abstract class ReplayMessage
//    {
//        public Guid ReplayID { get; set; }
//    }
//    [Obsolete]
//    public abstract class ReplayRequest : ReplayMessage
//    { }
//    [Obsolete]
//    public abstract class ReplayResponse : ReplayMessage
//    { }

//    #endregion

//    #region Generic Messages

//    /// <summary>
//    /// Represents an event being replayed.
//    /// </summary>
//    [Obsolete]
//    public class ReplayEvent : ReplayResponse
//    {
//        public IPersistedStreamEvent Event { get; set; }
//    }

//    /// <summary>
//    /// Requests the replay to be cancelled.
//    /// Cancellation happens is asynchronously, and the actor may receive messages even after the cancellation has been requested.
//    /// </summary>
//    [Obsolete]
//    public class CancelReplayRequest : ReplayRequest
//    { }

//    /// <summary>
//    /// Signals that the replay request was completed with success.
//    /// </summary>
//    [Obsolete]
//    public class ReplayCompleted : ReplayResponse
//    {
//        /// <summary>
//        /// The last checkpoint the reader saw before completing.
//        /// </summary>
//        public long LastSeenGlobalSequence { get; set; }
//    }

//    /// <summary>
//    /// Signals that the replay was cancelled has happened and no more messages should be sent for the replay.
//    /// Further messages for this replay after this message was received should be discarded.
//    /// </summary>
//    [Obsolete]
//    public class ReplayCancelled : ReplayResponse
//    { }

//    /// <summary>
//    /// Signals the replay was aborted for some reason.
//    /// Further messages for this replay after this message was received should be discarded.
//    /// </summary>
//    [Obsolete]
//    public class ReplayAborted : ReplayResponse
//    {
//        public Exception Exception { get; set; }
//    }

//    /// <summary>
//    /// Signals the event reader to stop replaying for some reason, and no more messages are required from the stream.
//    /// Once received, the event reader should stop sending messages and notify the replay has completed.
//    /// Further messages from this replay after this message was sent should be ignored by the sender.
//    /// </summary>
//    [Obsolete]
//    public class ReplayStopRequest : ReplayRequest
//    { }

//    #endregion

//    #region Stream Specific Messages

//    /// <summary>
//    /// Requests a replay for an aggregate.
//    /// </summary>]
//    [Obsolete]
//    public class ReplayAggregateRequest : ReplayRequest
//    {
//        public string StreamID { get; set; }
//        public int InitialSequence { get; set; }
//    }

//    #endregion

//    #region Projection Stream

//    [Obsolete]
//    public class ProjectionStreamReplayRequest : ReplayRequest
//    {
//        public string StreamID { get; set; }
//        public int InitialSequence { get; set; }
//        public bool SendIndexedEvents { get; set; }
//        public long MaxCheckpoint { get; set; } = Int64.MaxValue;
//        public int MaxEvents { get; internal set; } = Int32.MaxValue;
//    }

//    /// <summary>
//    /// Signals that no more events will be read from the index, and new messages
//    /// will require matching.
//    /// </summary>
//    [Obsolete]
//    public class ProjectionStreamIndexReplayCompleted : ReplayResponse
//    {
//        public int LastSeenProjectionStreamSequence { get; set; }
//        public long LastSeenGlobalSequence { get; set; }
//    }

//    [Obsolete]
//    public class ProjectionReplayCompleted : ReplayCompleted
//    {
//        /// <summary>
//        /// The last sequence the reader saw when replaying the projection.
//        /// </summary>
//        public int LastSeenProjectionStreamSequence { get; set; }
//    }

//    #endregion

//    public class GlobalSequenceRequest : ReplayRequest
//    { }

//    public class GlobalSequenceResponse : ReplayResponse
//    {
//        public long LastGlobalSequence { get; set; }
//    }

//    /// <summary>
//    /// Represents a request to replay events from the global stream.
//    /// </summary>
//    public class EventReplayRequest
//    {
//        public EventReplayRequest(long initialGlobalSequence, int count)
//        {
//            InitialGlobalSequence = initialGlobalSequence;
//            Count = count;
//        }

//        public Guid ReplayID { get; } = Guid.NewGuid();
//        public long InitialGlobalSequence { get; set; }
//        public int Count { get; set; }
//    }

//    public class EventReplayCompleted : ReplayResponse
//    { }
//}
