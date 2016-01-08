﻿using Akka.Actor;
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
    public class ReadStreamWorkerTests : EvenTestKit
    {
        const int TotalEventsA = 5;
        const int TotalEventsB = 10;
        const int TotalEventsC = 15;

        protected IActorRef CreateReader()
        {
            var list = new List<UnpersistedEvent>();

            for (var i = 0; i < TotalEventsA; i++)
                list.Add(new UnpersistedEvent("a", new object()));

            for (var i = 0; i < TotalEventsB; i++)
                list.Add(new UnpersistedEvent("b", new object()));

            for (var i = 0; i < TotalEventsC; i++)
                list.Add(new UnpersistedEvent("c", new object()));

            var store = new TestStore(list);
            var factory = new MockPersistedEventFactory();
            var props = Props.Create<ReadStreamWorker>(store, factory);

            return Sys.ActorOf(props);
        }

        [Theory]
        [InlineData("a", 1)]
        [InlineData("b", 3)]
        [InlineData("c", 5)]
        public void Reads_single_event(string streamName, int sequenceToRead)
        {
            var reader = CreateReader();

            var req = new ReadStreamRequest(streamName, sequenceToRead, 1);
            reader.Tell(req);

            ExpectMsg<ReadStreamResponse>(m => m.RequestID == req.RequestID && m.Event.Stream.Equals(streamName) && m.Event.StreamSequence == sequenceToRead);
            ExpectMsg<ReadStreamFinished>(m => m.RequestID == req.RequestID);
        }

        [Theory]
        [InlineData("a", TotalEventsA)]
        [InlineData("b", TotalEventsB)]
        [InlineData("c", TotalEventsC)]
        public void Reads_all_events(string streamName, int eventCount)
        {
            var reader = CreateReader();

            var req = new ReadStreamRequest(streamName, 1, EventCount.Unlimited);
            reader.Tell(req);

            for (int i = 1; i <= eventCount; i++)
                ExpectMsg<ReadStreamResponse>(m => m.RequestID == req.RequestID && m.Event.StreamSequence == i);

            ExpectMsg<ReadStreamFinished>(m => m.RequestID == req.RequestID);
        }

        [Fact]
        public void Reads_with_sequence_above_highest_sends_ReadFinished()
        {
            var reader = CreateReader();

            var req = new ReadStreamRequest("b", 1000, EventCount.Unlimited);
            reader.Tell(req);
            ExpectMsg<ReadStreamFinished>(m => m.RequestID == req.RequestID);
        }

        [Fact]
        public void Reads_with_count_zero_sends_ReadFinished()
        {
            var reader = CreateReader();

            var req = new ReadStreamRequest("c", 1, 0);

            reader.Tell(req);
            ExpectMsg<ReadStreamFinished>(m => m.RequestID == req.RequestID);
        }

        [Fact]
        public void Reads_with_initialsequence_and_counts()
        {
            var reader = CreateReader();

            var req = new ReadStreamRequest("c", 7, 3);
            reader.Tell(req);

            ExpectMsg<ReadStreamResponse>(m => m.RequestID == req.RequestID && m.Event.StreamSequence == 7);
            ExpectMsg<ReadStreamResponse>(m => m.RequestID == req.RequestID && m.Event.StreamSequence == 8);
            ExpectMsg<ReadStreamResponse>(m => m.RequestID == req.RequestID && m.Event.StreamSequence == 9);
            ExpectMsg<ReadStreamFinished>(m => m.RequestID == req.RequestID);
        }

        [Fact]
        public void Receives_ReadCancelled_on_cancel_request()
        {
            var reader = CreateReader();

            var req = new ReadStreamRequest("a", 1, EventCount.Unlimited);
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

            var props = Props.Create<ReadStreamWorker>(store, factory);
            var actor = Sys.ActorOf(props);

            var req = new ReadStreamRequest("a", 1, EventCount.Unlimited);
            actor.Tell(req);
            ExpectMsg<Aborted>(m => m.RequestID == req.RequestID && m.Exception == ex);
        }
    }
}
