using Akka.Actor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even
{
    public class CommandProcessorSupervisor : ReceiveActor
    {
        public static Props CreateProps(IActorRef writer, GlobalOptions options)
        {
            return Props.Create<CommandProcessorSupervisor>(writer, options);
        }

        public CommandProcessorSupervisor(IActorRef writer, GlobalOptions options)
        {

        }
    }
}
