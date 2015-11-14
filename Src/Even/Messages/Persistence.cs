using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even.Messages
{
    /// <summary>
    /// Represents a request to persist events to a stream.
    /// </summary>
    public class PersistenceRequest
    {
        public PersistenceRequest(IReadOnlyList<UnpersistedEvent> events)
        {
            Argument.Requires(events != null && events.Any(), nameof(events), "The argument must contain at least one event.");

            this.Events = events;
        }

        public PersistenceRequest(string streamId, int expectedStreamSequence, IReadOnlyList<UnpersistedEvent> events)
            : this(events)
        {
            Argument.Requires(!String.IsNullOrEmpty(streamId), nameof(streamId));
            Argument.Requires(expectedStreamSequence >= 0 || expectedStreamSequence == ExpectedSequence.Any, nameof(expectedStreamSequence));
            Argument.Requires(events.All(e => String.Equals(e.StreamID, streamId, StringComparison.OrdinalIgnoreCase)), nameof(events), $"All events must belong to the stream '{streamId}'");

            this.StreamID = streamId;
            this.ExpectedStreamSequence = expectedStreamSequence;
            this.Events = events;
        }

        public Guid PersistenceID { get; } = Guid.NewGuid();
        public string StreamID { get; }
        public int ExpectedStreamSequence { get; } = ExpectedSequence.Any;
        public IReadOnlyList<UnpersistedEvent> Events { get; }
    }

    public abstract class PersistenceResponse
    {
        public PersistenceResponse(Guid persistenceId)
        {
            PersistenceID = persistenceId;
        }

        public Guid PersistenceID { get; }
    }

    public class PersistenceSuccess : PersistenceResponse
    {
        public PersistenceSuccess(Guid persistenceId)
            : base(persistenceId)
        { }
    }

    public class PersistenceFailure : PersistenceResponse
    {
        public PersistenceFailure(Guid persistenceId, Exception ex, string reason = null)
            : base(persistenceId)
        {
            Exception = ex;
            _message = reason;
        }

        string _message;
        public string Reason => _message ?? Exception?.Message;
        public Exception Exception { get; private set; }
    }

    public class UnexpectedStreamSequence : PersistenceResponse
    {
        public UnexpectedStreamSequence(Guid persistenceId)
            : base(persistenceId)
        { }
    }

    public class DuplicatedEntry : PersistenceResponse
    {
        public DuplicatedEntry(Guid persistenceId)
            : base(persistenceId)
        { }
    }

    public class ProjectionIndexPersistenceRequest
    {
        public ProjectionIndexPersistenceRequest(string projectionStreamId, int projectionStreamSequence, long globalSequence)
        {
            Argument.Requires(projectionStreamId != null, nameof(projectionStreamId));
            Argument.Requires(projectionStreamSequence >= 0, nameof(projectionStreamSequence));
            Argument.Requires(globalSequence >= 0, nameof(globalSequence));

            this.ProjectionStreamID = projectionStreamId;
            this.ProjectionStreamSequence = projectionStreamSequence;
            this.GlobalSequence = globalSequence;
        }

        public string ProjectionStreamID { get; }
        public int ProjectionStreamSequence { get; }
        public long GlobalSequence { get; }
    }

    public class ProjectionIndexInconsistencyDetected
    { }

    public class ProjectionCheckpointPersistenceRequest
    {
        public ProjectionCheckpointPersistenceRequest(string streamId, long globalSequence)
        {
            Argument.Requires(streamId != null);
            Argument.Requires(globalSequence >= 0);

            this.StreamID = streamId;
            this.GlobalSequence = globalSequence;
        }

        public string StreamID { get; }
        public long GlobalSequence { get; }
    }
}
