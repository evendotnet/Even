using System;

namespace Even.Messages
{
    public class StartEventProcessor
    {
        public StartEventProcessor(Type eventProcessorType)
        {
            Argument.RequiresNotNull(eventProcessorType, nameof(eventProcessorType));

            this.EventProcessorType = eventProcessorType;
        }

        public Type EventProcessorType { get; }
    }
}
