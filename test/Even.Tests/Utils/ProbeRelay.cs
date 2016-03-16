using Akka.Actor;
using Akka.TestKit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even.Tests.Utils
{
    public class ProbeRelay : ReceiveActor
    {
        public ProbeRelay(IActorRef target)
        {
            ReceiveAny(o => target.Forward(o));
        }
    }

    public static class ProbeRelayExtensions
    {
        public static ProbeRelayObjects CreateTestRelay(this TestKitBase testKit)
        {
            var probe = testKit.CreateTestProbe();
            var props = Props.Create<ProbeRelay>(probe);

            return new ProbeRelayObjects
            {
                Props = props,
                Probe = probe
            };
        }
    }

    public class ProbeRelayObjects
    {
        public Props Props { get; set; }
        public TestProbe Probe { get; set; }
    }
}
