using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even.Messages
{
    public class PersistenceMessage
    {
        public Guid PersistenceID { get; set; }
    }

    public class PersistenceRequest : PersistenceMessage
    { }

    /// <summary>
    /// Represents a persistence request with no requirements for stream order.
    /// The events should just be appended to the end of the stream.
    /// </summary>
    public class EventPersistenceRequest : PersistenceRequest
    {
        public IReadOnlyCollection<IStreamEvent> Events { get; set; }
    }

    /// <summary>
    /// Represents a strict persistence request, where the stream sequence must match the expected sequence.
    /// This kind of request expects all events to belong to the same stream.
    /// </summary>
    public class StrictEventPersistenceRequest : PersistenceRequest
    {
        public string StreamID { get; set; }
        public int ExpectedStreamSequence { get; set; }
        public IReadOnlyCollection<IEvent> Events { get; set; }
    }

    public class AggregateSnapshotPersistenceRequest : PersistenceRequest
    {
        public IAggregateSnapshot Snapshot { get; set; }
    }

    public class ProjectionIndexPersistenceRequest : PersistenceRequest, IProjectionStreamIndex
    {
        public string ProjectionStreamID { get; set; }
        public long Checkpoint { get; set; }
        public int ProjectionSequence { get; set; }
    }

    public class PersistenceResponse : PersistenceMessage
    { }

    public class PersistenceSuccessful : PersistenceResponse
    { }

    public class PersistenceFailure : PersistenceResponse
    { }

    public class UnexpectedSequenceFailure : PersistenceFailure
    { }

    public class PersistenceUnknownError : PersistenceFailure
    {
        public Exception Exception { get; set; }
    }


}
