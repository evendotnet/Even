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
            }

            protected override Task PrepareToRebuild()
            {
                _probe.Tell("rebuild");
                return Task.CompletedTask;
            }
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
            string streamId = null;

            var sup = Sys.ActorOf(conf =>
            {
                conf.Receive<ProjectionSubscriptionRequest>((r, ctx) =>
                {
                    streamId = r.Query.ProjectionStreamID;
                    ctx.Sender.Tell(new ProjectionReplayFinished(r.RequestID));
                });
            });

            var props = Props.Create<TestProjection>(TestActor);
            var proj = Sys.ActorOf(props);

            proj.Tell(new InitializeProjection(sup, new GlobalOptions()));
            ExpectMsg<InitializationResult>(i => i.Initialized);

            var e = MockPersistedStreamEvent.Create(new TestEvent(), streamId: streamId);
            proj.Tell(e);

            ExpectMsg<IPersistedStreamEvent<TestEvent>>(TimeSpan.FromSeconds(15));
        }

        [Fact]
        public void Rebuilds_on_RebuildProjection_message()
        {
            string streamId = null;

            var sup = Sys.ActorOf(conf =>
            {
                conf.Receive<ProjectionSubscriptionRequest>((r, ctx) =>
                {
                    streamId = r.Query.ProjectionStreamID;
                    ctx.Sender.Tell(new ProjectionReplayFinished(r.RequestID));
                });
            });

            var props = Props.Create<TestProjection>(TestActor).WithSupervisorStrategy(new OneForOneStrategy(ex => Directive.Restart));
            var proj = Sys.ActorOf(props);

            proj.Tell(new InitializeProjection(sup, new GlobalOptions()));
            ExpectMsg<InitializationResult>(i => i.Initialized);

            proj.Tell(new RebuildProjection());

            ExpectMsg<string>(s => s == "rebuild");
        }
    }
}
