using Akka.Actor;
using Akka.TestKit;
using Akka.Actor.Dsl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Even.Messages;
using NSubstitute;
using Even.Tests.Mocks;
using Even.Tests.Utils;
using System.Threading;

namespace Even.Tests
{
    public class ProjectionTests : EvenTestKit
    {
        public class TestEvent { }
        public class TestQuery { }

        class TestProjection : Projection
        {
            IActorRef _probe;

            public TestProjection(IActorRef probe)
            {
                _probe = probe;

                OnEvent<TestEvent>(e =>
                {
                    _probe.Forward(e);
                });

                OnQuery<TestQuery>(q =>
                {
                    _probe.Forward(q);
                });
            }

            protected override void OnReady()
            {
                Receive<int>(async delay =>
                {
                    await Task.Delay(delay);
                });
            }

            protected override Task PrepareToRebuild()
            {
                _probe.Tell("rebuild");
                return Task.CompletedTask;
            }

            protected override Task OnExpiredQuery(object query)
            {
                _probe.Tell("expired");
                return Task.CompletedTask;
            }
        }

        private Stream _testStream;

        private IActorRef CreateAndInitializeTestProjection(GlobalOptions options = null)
        {
            var sup = Sys.ActorOf(conf =>
            {
                conf.Receive<ProjectionSubscriptionRequest>((r, ctx) =>
                {
                    _testStream = r.Query.ProjectionStream;
                    ctx.Sender.Tell(new ProjectionReplayFinished(r.RequestID));
                });
            });

            var props = Props.Create<TestProjection>(TestActor);
            var proj = Sys.ActorOf(props);

            proj.Tell(new InitializeProjection(sup, options ?? new GlobalOptions()));
            ExpectMsg<InitializationResult>(i => i.Initialized);

            return proj;
        }

        [Fact]
        public void Projection_requests_subscription_on_start()
        {
            var sup = CreateTestProbe();
            var proj = Sys.ActorOf<Projection>();
            proj.Tell(new InitializeProjection(sup, new GlobalOptions()));

            sup.ExpectMsg<ProjectionSubscriptionRequest>();
        }

        [Fact]
        public void Receives_events_after_replay()
        {
            var proj = CreateAndInitializeTestProjection();

            var e = MockPersistedStreamEvent.Create(new TestEvent(), stream: _testStream);
            proj.Tell(e);

            ExpectMsg<IPersistedStreamEvent<TestEvent>>(TimeSpan.FromSeconds(15));
        }

        [Fact]
        public void Rebuilds_on_RebuildProjection_message()
        {
            var proj = CreateAndInitializeTestProjection();

            proj.Tell(new RebuildProjection());

            ExpectMsg<string>(s => s == "rebuild");
        }

        [Fact]
        public void Handles_queries()
        {
            var proj = CreateAndInitializeTestProjection();

            var q = new TestQuery();
            Sys.EventStream.Publish(new Query<TestQuery>(CreateTestProbe(), q, Timeout.In(1000)));

            ExpectMsg<TestQuery>(o => o == q);
        }

        [Fact]
        public void Does_not_handle_expired_queries()
        {
            var proj = CreateAndInitializeTestProjection();

            // create an event to expire in 10 ms
            var q = new Query<TestQuery>(CreateTestProbe(), new TestQuery(), Timeout.In(10));

            // forces the projection to sleep for 100 ms
            proj.Tell(100);

            // publish the expired event
            Sys.EventStream.Publish(q);

            ExpectMsg<string>(s => s == "expired");
        }
    }
}
