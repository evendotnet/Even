using Akka.Actor;
using Even.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even
{
    public class ProjectionSupervisor : ReceiveActor
    {
        public ProjectionSupervisor()
        {
            Receive<InitializeProjectionSupervisor>(ini =>
            {
                _reader = ini.Reader;

                Become(ReceivingRequests);
            });
        }

        IActorRef _reader;

        private void ReceivingRequests()
        {
            Receive<ProjectionSubscriptionRequest>(ps =>
            {
                var key = "projection-" + ps.Query.StreamID;
                IActorRef pRef = Context.Child(key);

                // if the projection stream doesn't exist, start one
                if (pRef == null || pRef == ActorRefs.Nobody)
                {
                    pRef = Context.ActorOf<ProjectionStream>();
                    pRef.Tell(new InitializeProjectionStream { Query = ps.Query, EventReader = _reader });
                }

                pRef.Tell(ps);
            });
        }
    }
}
