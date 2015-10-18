using Akka.TestKit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Even.Tests
{
    public class EvenGatewayTests : EvenTestKit
    {
        TestProbe ReaderProbe;
        TestProbe WriterProbe;
        TestProbe AggregatesProbe;
        TestProbe CommandProcessorsProbe;
        TestProbe EventProcessorsProbe;
        TestProbe ProjectionsProbe;
        EvenGateway TestGateway;

        public EvenGatewayTests()
        {
            ReaderProbe = CreateTestProbe();
            WriterProbe = CreateTestProbe();
            AggregatesProbe = CreateTestProbe();
            CommandProcessorsProbe = CreateTestProbe();
            EventProcessorsProbe = CreateTestProbe();
            ProjectionsProbe = CreateTestProbe();
            InitializeTestGateway();
        }

        public void InitializeTestGateway(GlobalOptions options = null)
        {
            TestGateway = new EvenGateway(new EvenServices
            {
                Aggregates = AggregatesProbe,
                CommandProcessors = CommandProcessorsProbe,
                EventProcessors = EventProcessorsProbe,
                Projections = ProjectionsProbe,
                Reader = ReaderProbe,
                Writer = WriterProbe
            }, Sys, options ?? new GlobalOptions());
        }

        [Fact]
        public void Query_is_published_to_event_stream()
        {
            Sys.EventStream.Subscribe(TestActor, typeof(IQuery));

            var query = new object();
            TestGateway.Query(query);

            ExpectMsg<IQuery>(m => m.Message == query);
        }

        [Fact]
        public void Query_throws_TimeoutException_on_timeout()
        {
            Assert.ThrowsAsync<TimeoutException>(async () =>
            {
                await TestGateway.Query(new object(), TimeSpan.FromMilliseconds(10));
            });
        }
    }
}
