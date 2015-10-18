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
        GlobalOptions _options;
        IActorRef _projectionStreams;
        
        public static Props CreateProps(IActorRef projectionStreams, GlobalOptions options)
        {
            Argument.RequiresNotNull(projectionStreams, nameof(projectionStreams));
            Argument.RequiresNotNull(options, nameof(options));

            return Props.Create<ProjectionSupervisor>(projectionStreams, options);
        }

        public ProjectionSupervisor(IActorRef projectionStreamSupervisor, GlobalOptions options)
        {
            _projectionStreams = projectionStreamSupervisor;
            _options = options;

            Ready();
        }

        void Ready()
        {
            Receive<StartProjection>(sp =>
            {
                try
                {
                    var props = PropsFactory.Create(sp.ProjectionType);
                    var projection = Context.ActorOf(props);
                    projection.Ask(new InitializeProjection(_projectionStreams, _options)).PipeTo(Sender);
                }
                catch (Exception ex)
                {
                    Sender.Tell(InitializationResult.Failed(ex));
                }
            });
        }

        protected override SupervisorStrategy SupervisorStrategy()
        {
            return new OneForOneStrategy(ex =>
            {
                return Directive.Restart;
            });
        }
    }
}
