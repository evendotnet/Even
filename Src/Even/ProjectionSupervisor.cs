using Akka.Actor;
using Even.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even
{
    public class ProjectionSupervisor: ReceiveActor
    {
        public ProjectionSupervisor()
        {
            Receive<InitializeProjectionSupervisor>(ini =>
            {
                _streams = ini.Streams;

                Become(AcceptingProjections);
            });
        }

        IActorRef _streams;

        void AcceptingProjections()
        {
            Receive<StartProjection>(msg =>
            {
                var props = PropsFactory.Create(msg.ProjectionType);
                var actor = Context.ActorOf(props, msg.ProjectionID);

                actor.Tell(new InitializeProjection
                {
                    Streams = _streams
                });
            });
        }
    }
}
