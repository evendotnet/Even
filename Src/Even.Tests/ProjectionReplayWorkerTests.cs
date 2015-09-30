using Akka.Actor;
using Akka.Actor.Dsl;
using Even.Messages;
using Even.Tests.Mocks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Even.Tests
{
    public class ProjectionReplayWorkerTests : EvenTestKit
    {
        public class TestEvent { }

        [Fact]
        public void Requests_events_to_reader()
        {
            var request = new ProjectionSubscriptionRequest(new ProjectionStreamQuery(), 0);
            var worker = Sys.ActorOf<ProjectionReplayWorker>();
            worker.Tell(new InitializeProjectionReplayWorker(TestActor, ActorRefs.Nobody, request, 1, new GlobalOptions()));

            ExpectMsg<ReadIndexedProjectionStreamRequest>();
        }

        [Fact]
        public void Sends_read_events_to_subscriber()
        {
            var reader = Sys.ActorOf(conf =>
            {
                conf.Receive<ReadIndexedProjectionStreamRequest>((r, ctx) =>
                {
                    for (var i = 1; i <= 3; i++)
                    {
                        var e = MockPersistedStreamEvent.Create(new TestEvent(), i, i);
                        ctx.Sender.Tell(new ReadIndexedProjectionStreamResponse(r.RequestID, e));
                    }

                    ctx.Sender.Tell(new ReadIndexedProjectionStreamFinished(r.RequestID, 3));
                });
            });

            var request = new ProjectionSubscriptionRequest(new ProjectionStreamQuery(), 0);

            var worker = Sys.ActorOf<ProjectionReplayWorker>();
            worker.Tell(new InitializeProjectionReplayWorker(reader, TestActor, request, 3, new GlobalOptions()));

            ExpectMsg<ProjectionReplayEvent>(m => m.Event.StreamSequence == 1);
            ExpectMsg<ProjectionReplayEvent>(m => m.Event.StreamSequence == 2);
            ExpectMsg<ProjectionReplayEvent>(m => m.Event.StreamSequence == 3);
            ExpectMsg<ProjectionReplayFinished>();
        }

        [Fact]
        public void Worker_terminates_on_ReadFinished()
        {
            var reader = Sys.ActorOf(conf =>
            {
                conf.Receive<ReadIndexedProjectionStreamRequest>((r, ctx) =>
                {
                    ctx.Sender.Tell(new ReadIndexedProjectionStreamFinished(r.RequestID, 0));
                });
            });

            var request = new ProjectionSubscriptionRequest(new ProjectionStreamQuery(), 0);

            var worker = Sys.ActorOf<ProjectionReplayWorker>();
            worker.Tell(new InitializeProjectionReplayWorker(reader, ActorRefs.Nobody, request, 1, new GlobalOptions()));

            Watch(worker);
            ExpectTerminated(worker);
        }

        [Fact]
        public void Worker_requests_until_all_events_are_read()
        {
            var canFinish = false;

            var reader = Sys.ActorOf(conf =>
            {
                conf.Receive<ReadIndexedProjectionStreamRequest>((r, ctx) =>
                {
                    var start = r.InitialSequence;
                    int end;

                    if (canFinish)
                        end = start + r.Count;
                    else
                        end = start + 2;

                    for (var i = start; i <= end; i++)
                    {
                        var e = MockPersistedStreamEvent.Create(new TestEvent(), i, i);
                        ctx.Sender.Tell(new ReadIndexedProjectionStreamResponse(r.RequestID, e));
                    }

                    ctx.Sender.Tell(new ReadIndexedProjectionStreamFinished(r.RequestID, end));
                });
            });

            var request = new ProjectionSubscriptionRequest(new ProjectionStreamQuery(), 0);
            var options = new GlobalOptions { ProjectionReplayRetryInterval = TimeSpan.FromMilliseconds(600) };
            var worker = Sys.ActorOf<ProjectionReplayWorker>();
            worker.Tell(new InitializeProjectionReplayWorker(reader, TestActor, request, 5, new GlobalOptions()));

#pragma warning disable 4014
            Task.Delay(500).ContinueWith(_ => canFinish = true);
#pragma warning restore 4014

            ExpectMsg<ProjectionReplayEvent>(m => m.Event.StreamSequence == 1);
            ExpectMsg<ProjectionReplayEvent>(m => m.Event.StreamSequence == 2);
            ExpectMsg<ProjectionReplayEvent>(m => m.Event.StreamSequence == 3);
            ExpectMsg<ProjectionReplayEvent>(m => m.Event.StreamSequence == 4);
            ExpectMsg<ProjectionReplayEvent>(m => m.Event.StreamSequence == 5);
            ExpectMsg<ProjectionReplayFinished>();
        }
    }
}
