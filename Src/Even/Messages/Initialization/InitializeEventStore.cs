using Akka.Actor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even.Messages.Initialization
{
    public class InitializeEventStore
    {
        public EventStoreSettings Settings { get; set; }
        public IReadOnlyCollection<EventProcessorEntry> EventProcessors { get; set; }
    }

    public class EventStoreState
    {
        public bool Initialized { get; set; }
        public IActorRef CommandProcessors { get; set; }
        public IActorRef EventProcessors { get; set; }
    }
}
