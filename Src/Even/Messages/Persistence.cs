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
        public PersistenceRequest(IReadOnlyCollection<UnpersistedEvent> events)
        {
            Argument.Requires(events != null && events.Any(), nameof(events), "The argument must contain at least one event.");

            this.Events = events.ToList();
        }

        public PersistenceRequest(string streamId, int expectedStreamSequence, IReadOnlyCollection<UnpersistedEvent> events)
            : this(events)
        {
            Argument.Requires(!String.IsNullOrEmpty(streamId), nameof(streamId));
            Argument.Requires(expectedStreamSequence >= 0 || expectedStreamSequence == ExpectedSequence.Any, nameof(expectedStreamSequence));
            Argument.Requires(events.All(e => String.Equals(e.StreamID, streamId, StringComparison.OrdinalIgnoreCase)), nameof(events), $"All events must belong to the stream '{streamId}'");

            this.StreamID = streamId;
            this.ExpectedStreamSequence = expectedStreamSequence;
        }

        public Guid PersistenceID { get; } = Guid.NewGuid();
        public string StreamID { get; }
        public int ExpectedStreamSequence { get; } = ExpectedSequence.Any;
        public IReadOnlyCollection<UnpersistedEvent> Events { get; }
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
        public PersistenceFailure(Guid persistenceId, string message, Exception ex)
            : base(persistenceId)
        {
            Message = message;
            Exception = ex;
        }

        public string Message { get; private set; }
        public Exception Exception { get; private set; }
    }

    public class UnexpectedStreamSequence : PersistenceResponse
    {
        public UnexpectedStreamSequence(Guid persistenceId)
            : base(persistenceId)
        { }
    }

    public class DuplicatedEvent : PersistenceResponse
    {
        public DuplicatedEvent(Guid persistenceId)
            : base(persistenceId)
        { }
    }

    public class ProjectionIndexPersistenceRequest
    {
        public string ProjectionStreamID { get; set; }
        public int ProjectionStreamSequence { get; set; }
        public long GlobalSequence { get; set; }
    }
}
