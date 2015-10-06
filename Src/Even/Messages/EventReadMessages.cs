using System;

namespace Even.Messages
{
    #region Read

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

    public class ReadResponse
    {
        public ReadResponse(Guid requestId, IPersistedEvent @event)
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

    #endregion

    #region ReadStream

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

    public class ReadStreamResponse
    {
        public ReadStreamResponse(Guid requestId, IPersistedStreamEvent @event)
        {
            this.RequestID = requestId;
            this.Event = @event;
        }

        public Guid RequestID { get; }
        public IPersistedStreamEvent Event { get; }
    }

    public class ReadStreamFinished
    {
        public ReadStreamFinished(Guid requestId)
        {
            this.RequestID = requestId;
        }

        public Guid RequestID { get; }
    }

    #endregion

    #region ReadIndexedProjection

    public class ReadIndexedProjectionStreamRequest : IRequest
    {
        public ReadIndexedProjectionStreamRequest(string projectionStreamId, int initialSequence, int count)
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

    public class ReadIndexedProjectionStreamResponse
    {
        public ReadIndexedProjectionStreamResponse(Guid requestId, IPersistedStreamEvent @event)
        {
            this.RequestID = requestId;
            this.Event = @event;
        }

        public Guid RequestID { get; }
        public IPersistedStreamEvent Event { get; }
    }

    public class ReadIndexedProjectionStreamFinished
    {
        public ReadIndexedProjectionStreamFinished(Guid requestId, long lastSeenGlobalSequence)
        {
            this.RequestID = requestId;
            this.LastSeenGlobalSequence = lastSeenGlobalSequence;
        }

        public Guid RequestID { get; }
        public long LastSeenGlobalSequence { get; }
    }

    #endregion

    #region ReadProjectionIndexCheckpoint

    public class ReadProjectionIndexCheckpointRequest : IRequest
    {
        public ReadProjectionIndexCheckpointRequest(string projectionStreamId)
        {
            Argument.RequiresNotNull(projectionStreamId, nameof(projectionStreamId));
            this.ProjectionStreamID = projectionStreamId;
        }

        public Guid RequestID { get; } = Guid.NewGuid();
        public string ProjectionStreamID { get; }
    }

    public class ReadProjectionIndexCheckpointResponse
    {
        public ReadProjectionIndexCheckpointResponse(Guid requestId, long lastSeenGlobalSequence)
        {
            this.RequestID = requestId;
            this.LastSeenGlobalSequence = lastSeenGlobalSequence;
        }

        public Guid RequestID { get; }
        public long LastSeenGlobalSequence { get; }
    }

    #endregion

    #region ReadHighestGlobalSequence

    public class ReadHighestGlobalSequenceRequest : IRequest
    {
        public Guid RequestID { get; } = Guid.NewGuid();
    }

    public class ReadHighestGlobalSequenceResponse
    {
        public ReadHighestGlobalSequenceResponse(Guid requestId, long globalSequence)
        {
            this.RequestID = requestId;
            this.GlobalSequence = globalSequence;
        }

        public Guid RequestID { get; }
        public long GlobalSequence { get; }
    }

    #endregion
}
