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
    public class ReadWorkerTests : EvenTestKit
    {
        const int TotalEvents = 10;

        protected IActorRef CreateReader()
        {
            var list = new List<UnpersistedEvent>();

            for (var i = 0; i < TotalEvents; i++)
                list.Add(new UnpersistedEvent("a", new object()));

            var store = new TestStore(list);
            var factory = new MockPersistedEventFactory();
            var props = Props.Create<ReadWorker>(store, factory);

            return Sys.ActorOf(props);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(4)]
        [InlineData(10)]
        public void Reads_single_event(int sequenceToRead)
        {
            var reader = CreateReader();

            var req = new ReadRequest(sequenceToRead, 1);
            reader.Tell(req);

            ExpectMsg<ReadResponse>(m => m.RequestID == req.RequestID && m.Event.GlobalSequence == sequenceToRead);
            ExpectMsg<ReadFinished>(m => m.RequestID == req.RequestID);
        }

        [Fact]
        public void Reads_all_events()
        {
            var reader = CreateReader();
            
            var req = new ReadRequest(1, EventCount.Unlimited);

            reader.Tell(req);

            for (int i = 1; i <= TotalEvents; i++)
                ExpectMsg<ReadResponse>(m => m.RequestID == req.RequestID && m.Event.GlobalSequence == i);

            ExpectMsg<ReadFinished>(m => m.RequestID == req.RequestID);
        }

        [Fact]
        public void Reads_with_sequence_above_highest_sends_ReadFinished()
        {
            var reader = CreateReader();

            var req = new ReadRequest(1000, EventCount.Unlimited);
            reader.Tell(req);

            ExpectMsg<ReadFinished>(m => m.RequestID == req.RequestID);
        }

        [Fact]
        public void Reads_with_count_zero_sends_ReadFinished()
        {
            var reader = CreateReader();

            var req = new ReadRequest(1, 0);
            reader.Tell(req);

            ExpectMsg<ReadFinished>(m => m.RequestID == req.RequestID);
        }

        [Fact]
        public void Reads_with_initialsequence_and_counts()
        {
            var reader = CreateReader();

            var req = new ReadRequest(7, 3);
            reader.Tell(req);

            ExpectMsg<ReadResponse>(m => m.RequestID == req.RequestID && m.Event.GlobalSequence == 7);
            ExpectMsg<ReadResponse>(m => m.RequestID == req.RequestID && m.Event.GlobalSequence == 8);
            ExpectMsg<ReadResponse>(m => m.RequestID == req.RequestID && m.Event.GlobalSequence == 9);
            ExpectMsg<ReadFinished>(m => m.RequestID == req.RequestID);
        }

        [Fact]
        public void Receives_ReadCancelled_on_cancel_request()
        {
            var reader = CreateReader();

            var req = new ReadRequest(1, EventCount.Unlimited);
            reader.Tell(req);
            reader.Tell(new CancelReadRequest(req.RequestID));

            ExpectMsgEventually<ReadCancelled>(m => m.RequestID == req.RequestID);
        }

        [Fact]
        public void Receives_ReadAborted_on_store_exception()
        {
            var ex = new Exception();
            var store = MockEventStore.ThrowsOnReadStreams(ex);
            var factory = Substitute.For<IPersistedEventFactory>();

            var props = Props.Create<ReadWorker>(store, factory);
            var actor = Sys.ActorOf(props);

            var req = new ReadRequest(1, EventCount.Unlimited);
            actor.Tell(req);

            ExpectMsg<ReadAborted>(m => m.RequestID == req.RequestID && m.Exception == ex);
        }
    }
}
