using Akka.Actor;
using Even.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even
{
    public class EventProcessorSupervisor: ReceiveActor
    {
        Dictionary<string, IActorRef> _processors = new Dictionary<string, IActorRef>();
        GlobalOptions _options;

        public static Props CreateProps(GlobalOptions options)
        {
            return Props.Create<EventProcessorSupervisor>(options);
        }

        public EventProcessorSupervisor(GlobalOptions options)
        {
            Argument.Requires(options != null, nameof(options));
            _options = options;

            Ready();
        }

        void Ready()
        {
            Receive<StartEventProcessor>(m =>
            {
                
            });
        }

        class EventProcessorEntry
        {
            public EventProcessorEntry(string name, IActorRef actor)
            {

            }

            public IActorRef ActorRef;
            public string Name;
        }
    }
}
