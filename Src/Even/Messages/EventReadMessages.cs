using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even.Messages
{
    public class ReadRequest : IRequest
    {
        public ReadRequest(long initialGlobalSequence, int count)
        {
            Argument.Requires(initialGlobalSequence >= 1, nameof(initialGlobalSequence));
            Argument.Requires(count >= 0 || count == EventCount.Unlimited, nameof(count));

            this.InitialGlobalSequence = initialGlobalSequence;
            this.Count = count;
        }

        public Guid RequestID { get; } = Guid.NewGuid();
        public long InitialGlobalSequence { get; }
        public int Count { get; }
    }

    public class ReadStreamRequest : IRequest
    {
        public ReadStreamRequest(string streamId, int initialSequence, int count)
        {
            Argument.Requires(streamId != null);
            Argument.Requires(initialSequence >= 1, nameof(initialSequence));
            Argument.Requires(count >= 0 || count == EventCount.Unlimited, nameof(count));

            this.StreamID = streamId;
            this.InitialSequence = initialSequence;
            this.Count = count;
        }

        public Guid RequestID { get; } = Guid.NewGuid();
        public string StreamID { get; }
        public int InitialSequence { get; }
        public int Count { get; }
    }

    public class ReadIndexedProjectionRequest : IRequest
    {
        public ReadIndexedProjectionRequest(string projectionStreamId, int initialSequence, int count)
        {
            Argument.Requires(projectionStreamId != null);
            Argument.Requires(initialSequence >= 1, nameof(initialSequence));
            Argument.Requires(count >= 0 || count == EventCount.Unlimited, nameof(count));

            this.ProjectionStreamID = projectionStreamId;
            this.InitialSequence = initialSequence;
            this.Count = count;
        }

        public Guid RequestID { get; } = Guid.NewGuid();
        public string ProjectionStreamID { get; }
        public int InitialSequence { get; }
        public int Count { get; }
    }

    public class CancelReadRequest : IRequest
    {
        public CancelReadRequest(Guid requestId)
        {
            this.RequestID = requestId;
        }

        public Guid RequestID { get; }
    }

    public class ReadEventResponse
    {
        public ReadEventResponse(Guid requestId, IPersistedEvent @event)
        {
            this.RequestID = requestId;
            this.Event = @event;
        }

        public Guid RequestID { get; }
        public IPersistedEvent Event { get; }
    }

    public class ReadEventStreamResponse
    {
        public ReadEventStreamResponse(Guid requestId, IPersistedStreamEvent @event)
        {
            this.RequestID = requestId;
            this.Event = @event;
        }

        public Guid RequestID { get; }
        public IPersistedEvent Event { get; }
    }

    public class ReadFinished
    {
        public ReadFinished(Guid requestId)
        {
            this.RequestID = requestId;
        }

        public Guid RequestID { get; }
    }

    public class ReadProjectionIndexFinished
    {
        public ReadProjectionIndexFinished(Guid requestId, long lastSeenGlobalSequence)
        {
            this.RequestID = requestId;
            this.LastSeenGlobalSequence = lastSeenGlobalSequence;
        }

        public Guid RequestID { get; }
        public long LastSeenGlobalSequence { get; }
    }

    public class ReadCancelled
    {
        public ReadCancelled(Guid requestId)
        {
            this.RequestID = requestId;
        }

        public Guid RequestID { get; }
    }

    public class ReadAborted
    {
        public ReadAborted(Guid requestId, Exception exception)
        {
            this.RequestID = requestId;
            this.Exception = exception;
        }

        public Guid RequestID { get; }
        public Exception Exception { get; private set; }
    }

    // additional queries

    //public class HighestGlobalSequenceRequest : IRequest
    //{
    //}

    //public class HighestGlobalSequenceResponse
    //{
    //    public HighestGlobalSequenceResponse(long globalSequence)
    //    {
    //        this.GlobalSequence = globalSequence;
    //    }

    //    public long GlobalSequence { get; }
    //}
}
