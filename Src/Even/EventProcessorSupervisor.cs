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
        public EventProcessorSupervisor()
        {
            Receive<InitializeEventProcessorSupervisor>(ini =>
            {
                _projectionStreams = ini.ProjectionStreamSupervisor;

                Become(Ready);
            });
        }

        IActorRef _projectionStreams;

        void Ready()
        {
            Receive<StartEventProcessor>(s =>
            {
                var props = PropsFactory.Create(s.Type);
                var actor = Context.ActorOf(props, s.Name);

                actor.Tell(new InitializeEventProcessor
                {
                    ProjectionStreamSupervisor = _projectionStreams
                });
            });
        }
    }
}
