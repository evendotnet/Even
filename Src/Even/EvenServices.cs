using Akka.Actor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even
{
    public class EvenServices
    {
        public IActorRef Reader { get; internal set; }
        public IActorRef Writer { get; internal set; }
        public IActorRef Aggregates { get; internal set; }
        public IActorRef CommandProcessors { get; internal set; }
        public IActorRef EventProcessors { get; internal set; }
        public IActorRef Projections { get; internal set; }
    }
}
