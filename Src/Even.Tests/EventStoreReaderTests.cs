using Akka.Actor;
using Akka.TestKit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Even.Tests.Utils;
using Even.Messages;

namespace Even.Tests
{
    public class EventStoreReaderTests : EvenTestKit
    {
        class TestContainer
        {
            public TestContainer(EvenTestKit testKit)
            {
                var a = testKit.CreateTestRelay();
                var b = testKit.CreateTestRelay();
                var c = testKit.CreateTestRelay();
                var d = testKit.CreateTestRelay();

                ReadProbe = a.Probe;
                ReadStreamProbe = b.Probe;
                ReadIndexedProjectionProbe = c.Probe;
                ReadHighestGlobalSequenceProbe = d.Probe;

                var readerProps = EventStoreReader.CreateProps(a.Props, b.Props, c.Props, d.Props, new GlobalOptions());
                Reader = testKit.Sys.ActorOf(readerProps);
            }

            public IActorRef Reader { get; }
            public TestProbe ReadProbe { get; }
            public TestProbe ReadStreamProbe { get; }
            public TestProbe ReadIndexedProjectionProbe { get; }
            public TestProbe ReadHighestGlobalSequenceProbe { get; }
        }

        readonly TimeSpan DefaultTimeout = TimeSpan.FromMilliseconds(100);

        [Fact]
        public void ReadRequests_are_forwarded_to_worker()
        {
            var o = new TestContainer(this);

            var req = new ReadRequest(1, 1);
            o.Reader.Tell(req);

            o.ReadProbe.ExpectMsg<ReadRequest>(m => m == req);
        }

        [Fact]
        public void ReadStreamRequests_are_forwarded_to_worker()
        {
            var o = new TestContainer(this);

            var req = new ReadStreamRequest("a", 1, 1);
            o.Reader.Tell(req);

            o.ReadStreamProbe.ExpectMsg<ReadStreamRequest>(m => m == req);
        }

        [Fact]
        public void ReadIndexedProjectionStreamRequests_are_forwarded_to_worker()
        {
            var o = new TestContainer(this);

            var req = new ReadIndexedProjectionStreamRequest("a", 1, 1);
            o.Reader.Tell(req);

            o.ReadIndexedProjectionProbe.ExpectMsg<ReadIndexedProjectionStreamRequest>(m => m == req);
        }

        [Fact]
        public void ReadHighestGlobalSequenceRequests_are_fowarded_to_worker()
        {
            var o = new TestContainer(this);

            var req = new ReadHighestGlobalSequenceRequest();
            o.Reader.Tell(req);
            o.ReadHighestGlobalSequenceProbe.ExpectMsg<ReadHighestGlobalSequenceRequest>(m => m == req);
        }
    }
}
