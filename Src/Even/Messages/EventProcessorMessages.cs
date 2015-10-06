using System;

namespace Even.Messages
{
    public class StartEventProcessor
    {
        public StartEventProcessor(Type eventProcessorType, string name = null)
        {
            Argument.RequiresNotNull(eventProcessorType, nameof(eventProcessorType));
            Argument.Requires(eventProcessorType.IsAssignableFrom(eventProcessorType));

            this.EventProcessorType = eventProcessorType;
            this.Name = name ?? eventProcessorType.FullName;
        }

        public Type EventProcessorType { get; }
        public string Name { get; }
    }
}
