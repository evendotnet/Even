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
    public class ProjectionStreamTests : EvenTestKit
    {
        public class TestEvent { }

        class TestContainer
        {
            public TestContainer(TestKitBase testKit, GlobalOptions options = null, Type replayWorkerType = null)
            {
                var query = new ProjectionStreamQuery();
                Reader = testKit.CreateTestProbe();
                Writer = testKit.CreateTestProbe();
                options = options ?? new GlobalOptions();

                var props = Even.ProjectionStream.CreateProps(query, Reader, Writer, options);
                ProjectionStream = testKit.Sys.ActorOf(props);
            }

            public IActorRef ProjectionStream;
            public TestProbe Reader;
            public TestProbe Writer;
        }

        IActorRef CreateWorkingReader(int eventCount = 0)
        {
            return Sys.ActorOf(conf =>
            {
                conf.Receive<ReadProjectionIndexCheckpointRequest>((r, ctx) =>
                {
                    ctx.Sender.Tell(new ReadProjectionIndexCheckpointResponse(r.RequestID, eventCount));
                });

                conf.Receive<ReadRequest>((r, ctx) =>
                {
                    for (var i = 0; i < eventCount; i++)
                    {
                        var domainEvent = new TestEvent();
                        var e = MockPersistedEvent.Create(new TestEvent(), i + 1);
                        ctx.Sender.Tell(new ReadResponse(r.RequestID, e));
                    }

                    ctx.Sender.Tell(new ReadFinished(r.RequestID));
                });
            });
        }

        [Fact]
        public void Requests_checkpoint_on_start()
        {
            var reader = CreateTestProbe();
            var props = ProjectionStream.CreateProps(new ProjectionStreamQuery(), reader, ActorRefs.Nobody, new GlobalOptions());
            Sys.ActorOf(props);

            reader.ExpectMsg<ReadProjectionIndexCheckpointRequest>();
        }

        [Fact]
        public void Requests_events_after_checkpoint()
        {
            var reader = Sys.ActorOf(conf =>
            {
                conf.Receive<ReadProjectionIndexCheckpointRequest>((r, ctx) => ctx.Sender.Tell(new ReadProjectionIndexCheckpointResponse(r.RequestID, 0)));
                conf.Receive<ReadRequest>((r, ctx) => TestActor.Forward(r));
            });

            var props = ProjectionStream.CreateProps(new ProjectionStreamQuery(), reader, ActorRefs.Nobody, new GlobalOptions());
            Sys.ActorOf(props);

            ExpectMsg<ReadRequest>();
        }

        [Fact]
        public void Throws_on_checkpoint_abort()
        {
            var reader = Sys.ActorOf(conf =>
            {
                conf.Receive<ReadProjectionIndexCheckpointRequest>((r, ctx) => ctx.Sender.Tell(new Aborted(r.RequestID, new Exception())));
            });

            var props = ProjectionStream.CreateProps(new ProjectionStreamQuery(), reader, ActorRefs.Nobody, new GlobalOptions());

            Sys.ActorOf(c =>
            {
                c.OnPreStart = ctx => ctx.ActorOf(props);
                c.Strategy = new OneForOneStrategy(ex =>
                {
                    TestActor.Tell(ex);
                    return Directive.Stop;
                });
            });

            ExpectMsg<Exception>();
        }

        [Fact]
        public void Throws_on_read_abort()
        {
            var reader = Sys.ActorOf(conf =>
            {
                conf.Receive<ReadProjectionIndexCheckpointRequest>((r, ctx) => ctx.Sender.Tell(new ReadProjectionIndexCheckpointResponse(r.RequestID, 0)));
                conf.Receive<ReadRequest>((r, ctx) => ctx.Sender.Tell(new Aborted(r.RequestID, new Exception())));
            });

            var props = ProjectionStream.CreateProps(new ProjectionStreamQuery(), reader, ActorRefs.Nobody, new GlobalOptions());

            Sys.ActorOf(c =>
            {
                c.OnPreStart = ctx => ctx.ActorOf(props);
                c.Strategy = new OneForOneStrategy(ex =>
                {
                    TestActor.Tell(ex);
                    return Directive.Stop;
                });
            });

            ExpectMsg<Exception>();
        }

        [Fact]
        public void Throws_on_checkpoint_timeout()
        {
            var options = new GlobalOptions
            {
                ReadRequestTimeout = TimeSpan.FromMilliseconds(300)
            };

            var props = ProjectionStream.CreateProps(new ProjectionStreamQuery(), ActorRefs.Nobody, ActorRefs.Nobody, options);

            Sys.ActorOf(c =>
            {
                c.OnPreStart = ctx => ctx.ActorOf(props);
                c.Strategy = new OneForOneStrategy(ex =>
                {
                    TestActor.Tell(ex);
                    return Directive.Stop;
                });
            });

            ExpectMsg<Exception>();
        }

        [Fact]
        public void Throws_on_read_timeout()
        {
            bool checkpointRead = false;

            var reader = Sys.ActorOf(conf =>
            {
                conf.Receive<ReadProjectionIndexCheckpointRequest>((r, ctx) =>
                {
                    ctx.Sender.Tell(new ReadProjectionIndexCheckpointResponse(r.RequestID, 0));
                    checkpointRead = true;
                });
            });

            var options = new GlobalOptions
            {
                ReadRequestTimeout = TimeSpan.FromMilliseconds(300)
            };

            var props = ProjectionStream.CreateProps(new ProjectionStreamQuery(), reader, ActorRefs.Nobody, options);

            Sys.ActorOf(c =>
            {
                c.OnPreStart = ctx => ctx.ActorOf(props);
                c.Strategy = new OneForOneStrategy(ex =>
                {
                    TestActor.Tell(ex);
                    return Directive.Stop;
                });
            });

            ExpectMsg<Exception>();
            Assert.True(checkpointRead);
        }

        [Fact]
        public void Sends_ProjectionReplayFinished_to_uptodate_subscriber()
        {
            var reader = CreateWorkingReader();
            var query = new ProjectionStreamQuery();
            var props = ProjectionStream.CreateProps(query, reader, ActorRefs.Nobody, new GlobalOptions());
            var ps = Sys.ActorOf(props);
            var subscriber = CreateTestProbe();

            ps.Tell(new ProjectionSubscriptionRequest(query, 0), subscriber);
            subscriber.ExpectMsg<ProjectionReplayFinished>();
        }

        [Fact]
        public void Sends_RebuildProjection_to_subscriber_with_unknown_sequence()
        {
            var reader = CreateWorkingReader(0);
            var query = new ProjectionStreamQuery();
            var props = ProjectionStream.CreateProps(query, reader, ActorRefs.Nobody, new GlobalOptions());
            var ps = Sys.ActorOf(props);
            var subscriber = CreateTestProbe();

            // projection stream only knows event 0, projection says it knows 99
            ps.Tell(new ProjectionSubscriptionRequest(query, 99), subscriber);
            subscriber.ExpectMsg<RebuildProjection>();
        }

        [Fact]
        public void Subscribers_that_received_RebuildProjection_need_to_resubscribe_to_receive_messages()
        {
            var reader = CreateWorkingReader();
            var query = new ProjectionStreamQuery(new DomainEventPredicate(typeof(TestEvent)));
            var props = ProjectionStream.CreateProps(query, reader, ActorRefs.Nobody, new GlobalOptions());
            var ps = Sys.ActorOf(props);

            var subscriber = Sys.ActorOf(conf =>
            {
                conf.Receive<RebuildProjection>((r, ctx) => { });
                conf.ReceiveAny((o, ctx) => TestActor.Forward(o));
            });

            // subscribe with invalid sequence
            ps.Tell(new ProjectionSubscriptionRequest(query, 10), subscriber);

            //publish to event stream
            var e = MockPersistedEvent.Create(new TestEvent());
            Sys.EventStream.Publish(e);

            // expects the domain event to be the same
            ExpectNoMsg(TimeSpan.FromSeconds(1));
        }

        [Fact]
        public void Publishes_events_from_eventstream_to_subscribers()
        {
            var reader = CreateWorkingReader();
            var query = new ProjectionStreamQuery(new DomainEventPredicate(typeof(TestEvent)));
            var props = ProjectionStream.CreateProps(query, reader, ActorRefs.Nobody, new GlobalOptions());
            var ps = Sys.ActorOf(props);

            var subscriber = Sys.ActorOf(conf =>
            {
                conf.Receive<ProjectionReplayFinished>((r, ctx) => { });
                conf.ReceiveAny((o, ctx) => TestActor.Forward(o));
            });

            // subscribe
            ps.Tell(new ProjectionSubscriptionRequest(query, 0), subscriber);

            //publish to event stream
            var e = MockPersistedEvent.Create(new TestEvent());
            Sys.EventStream.Publish(e);

            // expects the domain event to be the same
            ExpectMsg<IPersistedStreamEvent>(m => m.DomainEvent == e.DomainEvent);
        }

        [Fact]
        public void SubscriptionRequest_with_past_sequence_starts_projection_replay_worker()
        {
            var workerProps = Props.Create<ProbeRelay>(TestActor);

            var reader = CreateWorkingReader(1);
            var query = new ProjectionStreamQuery(new DomainEventPredicate(typeof(TestEvent)));
            var props = ProjectionStream.CreateProps(query, reader, ActorRefs.Nobody, new GlobalOptions(), workerProps);
            var ps = Sys.ActorOf(props);

            var subscriber = CreateTestProbe();

            ps.Tell(new ProjectionSubscriptionRequest(query, 0), subscriber);

            ExpectMsg<InitializeProjectionReplayWorker>();
        }

        [Fact]
        public async Task Sends_ProjectionUnsubscribed_to_subscribers_on_restart()
        {
            var query = new ProjectionStreamQuery();
            var reader = CreateWorkingReader();
            var props = ProjectionStream.CreateProps(query, reader, ActorRefs.Nobody, new GlobalOptions());
            var ps = Sys.ActorOf(props);

            var subscriber = Sys.ActorOf(conf =>
            {
                conf.Receive<ProjectionReplayFinished>((m, ctx) => { });
                conf.ReceiveAny((o, ctx) => TestActor.Forward(o));
            });

            ps.Tell(new ProjectionSubscriptionRequest(query, 0), subscriber);
            await Task.Delay(100);
            ps.Tell(Kill.Instance);

            ExpectMsg<ProjectionUnsubscribed>();
        }

        [Fact]
        public void Requests_matched_events_be_written_to_index()
        {
            var query = new ProjectionStreamQuery(new DomainEventPredicate(typeof(TestEvent)));
            var writer = CreateTestProbe();
            var props = ProjectionStream.CreateProps(query, CreateWorkingReader(), writer, new GlobalOptions());
            var ps = Sys.ActorOf(props);

            ps.Tell(MockPersistedEvent.Create(new object(), 1));
            ps.Tell(MockPersistedEvent.Create(new TestEvent(), 2));
            ps.Tell(MockPersistedEvent.Create(new object(), 3));
            ps.Tell(MockPersistedEvent.Create(new TestEvent(), 4));
            ps.Tell(MockPersistedEvent.Create(new object(), 5));

            writer.ExpectMsg<ProjectionIndexPersistenceRequest>(m => m.ProjectionStreamID == query.ProjectionStreamID && m.ProjectionStreamSequence == 1 && m.GlobalSequence == 2);
            writer.ExpectMsg<ProjectionIndexPersistenceRequest>(m => m.ProjectionStreamID == query.ProjectionStreamID && m.ProjectionStreamSequence == 2 && m.GlobalSequence == 4);
            writer.ExpectNoMsg(TimeSpan.FromMilliseconds(500));
        }
    }
}
