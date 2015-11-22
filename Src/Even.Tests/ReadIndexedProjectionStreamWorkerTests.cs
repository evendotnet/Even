using Akka.Actor;
using Even.Messages;
using Even.Tests.Mocks;
using NSubstitute;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Even.Tests
{
    public class ReadIndexedProjectionStreamWorkerTests : EvenTestKit
    {
        const int TotalEventsA = 4;
        const int TotalEventsB = 6;
        const int TotalEventsC = 10;

        protected IActorRef CreateReader()
        {
            var list = new List<UnpersistedEvent>();

            for (var i = 0; i < 30; i++)
                list.Add(new UnpersistedEvent("a", new object()));

            var store = new TestStore(list);

            Task.WhenAll(
                store.WriteProjectionIndexAsync("a", 0, new long[] { 2, 4, 6, 8}),
                store.WriteProjectionIndexAsync("b", 0, new long[] { 5, 10, 15, 20, 25, 30 }),
                store.WriteProjectionIndexAsync("c", 0, new long[] { 3, 6, 9, 12, 15, 18, 21, 24, 27, 30 })
            ).Wait();

            var factory = new MockPersistedEventFactory();
            var props = Props.Create<ReadIndexedProjectionStreamWorker>(store, factory);

            return Sys.ActorOf(props);
        }

        [Theory]
        [InlineData("a", 1)]
        [InlineData("b", 3)]
        [InlineData("c", 5)]
        public void Reads_single_event(string streamName, int sequenceToRead)
        {
            var reader = CreateReader();

            var req = new ReadIndexedProjectionStreamRequest(streamName, sequenceToRead, 1);
            reader.Tell(req);

            ExpectMsg<ReadIndexedProjectionStreamResponse>(m => m.RequestID == req.RequestID && m.Event.StreamSequence == sequenceToRead);
            ExpectMsg<ReadIndexedProjectionStreamFinished>(m => m.RequestID == req.RequestID);
        }

        [Theory]
        [InlineData("a", TotalEventsA)]
        [InlineData("b", TotalEventsB)]
        [InlineData("c", TotalEventsC)]
        public void Reads_all_events(string streamName, int eventCount)
        {
            var reader = CreateReader();

            var req = new ReadIndexedProjectionStreamRequest(streamName, 1, EventCount.Unlimited);
            reader.Tell(req);

            for (int i = 1; i <= eventCount; i++)
                ExpectMsg<ReadIndexedProjectionStreamResponse>(m => m.RequestID == req.RequestID && m.Event.StreamSequence == i);

            ExpectMsg<ReadIndexedProjectionStreamFinished>(m => m.RequestID == req.RequestID);
        }

        [Fact]
        public void Reads_with_sequence_above_highest_sends_ReadFinished()
        {
            var reader = CreateReader();

            var req = new ReadIndexedProjectionStreamRequest("b", 1000, EventCount.Unlimited);
            reader.Tell(req);
            ExpectMsg<ReadIndexedProjectionStreamFinished>(m => m.RequestID == req.RequestID);
        }

        [Fact]
        public void Reads_with_count_zero_sends_ReadFinished()
        {
            var reader = CreateReader();

            var req = new ReadIndexedProjectionStreamRequest("c", 1, 0);

            reader.Tell(req);
            ExpectMsg<ReadIndexedProjectionStreamFinished>(m => m.RequestID == req.RequestID);
        }

        [Fact]
        public void Reads_with_initialsequence_and_counts()
        {
            var reader = CreateReader();

            var req = new ReadIndexedProjectionStreamRequest("c", 7, 3);
            reader.Tell(req);

            ExpectMsg<ReadIndexedProjectionStreamResponse>(m => m.RequestID == req.RequestID && m.Event.StreamSequence == 7);
            ExpectMsg<ReadIndexedProjectionStreamResponse>(m => m.RequestID == req.RequestID && m.Event.StreamSequence == 8);
            ExpectMsg<ReadIndexedProjectionStreamResponse>(m => m.RequestID == req.RequestID && m.Event.StreamSequence == 9);
            ExpectMsg<ReadIndexedProjectionStreamFinished>(m => m.RequestID == req.RequestID);
        }

        [Fact]
        public void Receives_ReadCancelled_on_cancel_request()
        {
            var reader = CreateReader();

            var req = new ReadIndexedProjectionStreamRequest("a", 1, EventCount.Unlimited);
            reader.Tell(req);
            reader.Tell(new CancelRequest(req.RequestID));

            ExpectMsgEventually<Cancelled>(m => m.RequestID == req.RequestID);
        }

        [Fact]
        public void Receives_ReadAborted_on_store_exception()
        {
            var ex = new Exception();
            var store = MockEventStore.ThrowsOnReadStreams(ex);
            var factory = Substitute.For<IPersistedEventFactory>();

            var props = Props.Create<ReadIndexedProjectionStreamWorker>(store, factory);
            var actor = Sys.ActorOf(props);

            var req = new ReadIndexedProjectionStreamRequest("a", 1, EventCount.Unlimited);
            actor.Tell(req);
            ExpectMsg<Aborted>(m => m.RequestID == req.RequestID && m.Exception == ex);
        }
    }

}
