using System;

namespace Even.Messages
{
    public class StartProjection
    {
        public StartProjection(Type projectionType, string name)
        {
            Argument.RequiresNotNull(projectionType, nameof(projectionType));
            Argument.Requires(typeof(Projection).IsAssignableFrom(projectionType), nameof(projectionType));

            this.ProjectionType = projectionType;
            this.Name = name ?? projectionType.FullName;
        }

        public Type ProjectionType { get; }
        public string Name { get; }
    }

    public class ProjectionSubscriptionRequest : IRequest
    {
        public ProjectionSubscriptionRequest(ProjectionStreamQuery query, int lastKnownSequence)
        {
            Argument.RequiresNotNull(query, nameof(query));
            Argument.Requires(lastKnownSequence >= 0, nameof(lastKnownSequence));

            Query = query;
            LastKnownSequence = lastKnownSequence;
        }

        public Guid RequestID { get; } = Guid.NewGuid();
        public ProjectionStreamQuery Query { get; }
        public int LastKnownSequence { get; }
    }

    public class ProjectionReplayEvent
    {
        public ProjectionReplayEvent(Guid requestId, IPersistedStreamEvent persistedEvent)
        {
            this.RequestID = requestId;
            this.Event = persistedEvent;
        }

        public Guid RequestID { get; }
        public IPersistedStreamEvent Event { get; }
    }

    public class ProjectionReplayFinished
    {
        public ProjectionReplayFinished(Guid requestId)
        {
            this.RequestID = requestId;
        }

        public Guid RequestID { get; }
    }

    public class RebuildProjection
    { }

    public class ProjectionUnsubscribed
    { }
}
