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
        IActorRef _projectionStreamsSupervisor;
        
        public ProjectionSupervisor(IActorRef projectionStreamSupervisor, GlobalOptions options)
        {
            _projectionStreamsSupervisor = projectionStreamSupervisor;
            _options = options;
        }

        void Ready()
        {
            Receive<StartProjection>(sp =>
            {
                var props = PropsFactory.Create(sp.ProjectionType);
                var projection = Context.ActorOf(props);

                Task.Run(async () =>
                {
                    var result = await projection.Ask(new InitializeProjection(_projectionStreamsSupervisor, _options)) as InitializationResult;

                    if (result.Initialized)
                    {

                    }

                });
                
            });
        }

        protected override SupervisorStrategy SupervisorStrategy()
        {
            return new OneForOneStrategy(ex =>
            {
                return Directive.Restart;
            });
        }

        class ProjectionInfo
        {
            public IActorRef ActorRef;
            public string Name;
        }
    }
}
