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
        public PersistenceRequest(string streamId, int expectedStreamSequence, IEnumerable<UnpersistedEvent> events, bool allowBuffering = false)
        {
            Contract.Requires(!String.IsNullOrEmpty(streamId));
            Contract.Requires(expectedStreamSequence >= 0);
            Contract.Requires(events != null && events.Any());

            this.PersistenceID = Guid.NewGuid();
            this.StreamID = streamId;
            this.ExpectedStreamSequence = expectedStreamSequence;
            this.Events = events.ToList();
            this.UseWriteBuffer = allowBuffering && expectedStreamSequence == 0;
        }

        public Guid PersistenceID { get; }
        public string StreamID { get; }
        public int ExpectedStreamSequence { get; }
        public IReadOnlyCollection<UnpersistedEvent> Events { get; }
        public bool UseWriteBuffer { get; }
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

    public class ProjectionIndexPersistenceRequest
    {
        public string ProjectionStreamID { get; set; }
        public IndexSequenceEntry Entry { get; set; }
    }
}
