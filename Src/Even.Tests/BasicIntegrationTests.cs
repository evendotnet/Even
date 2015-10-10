using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Even.Tests
{
    public class BasicIntegrationTests : EvenTestKit
    {
        public class DoSomething {}
        public class SomethingDone { public int Sequence { get; set; } }

        public class TestAggregate : Aggregate
        {
            int sequence = 0;

            public TestAggregate()
            {
                OnCommand<DoSomething>(c =>
                {
                    Persist(new SomethingDone { Sequence = sequence + 1 });
                });

                OnEvent<SomethingDone>(e =>
                {
                    sequence = e.Sequence;
                });
            }
        }

        [Fact]
        public async Task Event_is_accepted_and_published()
        {
            Sys.EventStream.Subscribe(TestActor, typeof(IPersistedEvent<SomethingDone>));
            var gateway = await Sys.SetupEven().Start();
            var response = await gateway.SendAggregateCommand<TestAggregate>(new Guid(), new DoSomething());

            Assert.True(response.Accepted);

            ExpectMsg<IPersistedEvent<SomethingDone>>();
        }
    }
}
