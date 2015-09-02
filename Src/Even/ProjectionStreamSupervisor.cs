using Akka.Actor;
using Even.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even
{
    public class ProjectionStreams : ReceiveActor
    {
        public ProjectionStreams()
        {
            Receive<InitializeProjectionStreams>(ini =>
            {
                _reader = ini.Reader;
                _indexWriter = ini.Writer;

                Become(ReceivingRequests);
            });
        }

        IActorRef _reader;
        IActorRef _indexWriter;

        private void ReceivingRequests()
        {
            Receive<ProjectionSubscriptionRequest>(ps =>
            {
                var key = "projection-" + ps.Query.ProjectionStreamID;
                IActorRef pRef = Context.Child(key);

                // if the projection stream doesn't exist, start one
                if (pRef == null || pRef == ActorRefs.Nobody)
                {
                    pRef = Context.ActorOf<ProjectionStream>();
                    pRef.Tell(new InitializeProjectionStream {
                        Query = ps.Query,
                        Reader = _reader,
                        Writer = _indexWriter
                    });
                }

                pRef.Forward(ps);
            });
        }
    }
}
