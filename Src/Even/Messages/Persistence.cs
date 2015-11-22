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

        public PersistenceRequest(Stream stream, int expectedStreamSequence, IReadOnlyList<UnpersistedEvent> events)
            : this(events)
        {
            Argument.RequiresNotNull(stream, nameof(stream));
            Argument.Requires(expectedStreamSequence >= 0 || expectedStreamSequence == ExpectedSequence.Any, nameof(expectedStreamSequence));
            Argument.Requires(events.All(e => e.Stream.Equals(stream)), nameof(events), $"All events must belong to the stream '{stream}'");

            this.Stream = stream;
            this.ExpectedStreamSequence = expectedStreamSequence;
            this.Events = events;
        }

        public Guid PersistenceID { get; } = Guid.NewGuid();
        public Stream Stream { get; }
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
        public ProjectionIndexPersistenceRequest(Stream projectionStream, int projectionStreamSequence, long globalSequence)
        {
            Argument.Requires(projectionStream != null, nameof(projectionStream));
            Argument.Requires(projectionStreamSequence >= 0, nameof(projectionStreamSequence));
            Argument.Requires(globalSequence >= 0, nameof(globalSequence));

            this.ProjectionStream = projectionStream;
            this.ProjectionStreamSequence = projectionStreamSequence;
            this.GlobalSequence = globalSequence;
        }

        public Stream ProjectionStream { get; }
        public int ProjectionStreamSequence { get; }
        public long GlobalSequence { get; }
    }

    public class ProjectionIndexInconsistencyDetected
    { }

    public class ProjectionCheckpointPersistenceRequest
    {
        public ProjectionCheckpointPersistenceRequest(Stream stream, long globalSequence)
        {
            Argument.Requires(stream != null);
            Argument.Requires(globalSequence >= 0);

            this.Stream = stream;
            this.GlobalSequence = globalSequence;
        }

        public Stream Stream { get; }
        public long GlobalSequence { get; }
    }
}
