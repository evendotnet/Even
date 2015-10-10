using Akka.Actor;
using Even.Messages;
using Even.Tests.Mocks;
using System;
using System.Collections.Generic;
using Xunit;

namespace Even.Tests
{
    public class SerialEventStreamWriterTests : EvenTestKit
    {
        #region Helpers

        class SampleEvent1 { }
        class SampleEvent2 { }
        class SampleEvent3 { }

        protected PersistenceRequest CreatePersistenceRequest(int eventCount = 1)
        {
            return CreatePersistenceRequest(Guid.NewGuid().ToString(), ExpectedSequence.Any, eventCount);
        }

        protected PersistenceRequest CreatePersistenceRequest(string streamId, int expectedSequence, int eventCount)
        {
            var list = new List<UnpersistedEvent>();

            for (var i = 0; i < eventCount; i++)
                list.Add(new UnpersistedEvent(streamId, new SampleEvent1()));

            return new PersistenceRequest(streamId, expectedSequence, list);
        }

        protected IActorRef CreateWriter(IEventStoreWriter writer = null, ISerializer serializer = null, IActorRef dispatcher = null)
        {
            writer = writer ?? MockEventStore.SuccessfulWriter();
            serializer = serializer ?? new MockSerializer();
            dispatcher = dispatcher ?? CreateTestProbe();

            var props = Props.Create<SerialEventStreamWriter>(writer, serializer, dispatcher);
            return Sys.ActorOf(props);
        }

        #endregion

        [Fact]
        public void Writer_tells_persistedevents_to_dispatcher_in_order()
        {
            var dispatcher = CreateTestProbe();
            var writer = CreateWriter(writer: MockEventStore.SuccessfulWriter(), dispatcher: dispatcher);

            var request = new PersistenceRequest(new[] {
                new UnpersistedEvent("a", new SampleEvent3()),
                new UnpersistedEvent("a", new SampleEvent1()),
                new UnpersistedEvent("a", new SampleEvent2())
            });

            writer.Tell(request);

            dispatcher.ExpectMsg<IPersistedEvent<SampleEvent3>>();
            dispatcher.ExpectMsg<IPersistedEvent<SampleEvent1>>();
            dispatcher.ExpectMsg<IPersistedEvent<SampleEvent2>>();
            dispatcher.ExpectNoMsg(50);
        }

        [Fact]
        public void UnexpectedStreamSequenceException_causes_unexpectedstreamsequence_message()
        {
            var writer = CreateWriter(writer: MockEventStore.ThrowsOnWrite<UnexpectedStreamSequenceException>());

            var request = CreatePersistenceRequest();
            writer.Tell(request);

            ExpectMsg<UnexpectedStreamSequence>(msg => msg.PersistenceID == request.PersistenceID);
        }

        [Fact]
        public void DuplicatedEventException_causes_duplicatedevent_message()
        {
            var writer = CreateWriter(writer: MockEventStore.ThrowsOnWrite<DuplicatedEntryException>());
            var request = CreatePersistenceRequest();
            writer.Tell(request);

            ExpectMsg<DuplicatedEntry>(msg => msg.PersistenceID == request.PersistenceID);
        }

        [Theory]
        [InlineData(typeof(ArgumentException))]
        [InlineData(typeof(TimeoutException))]
        [InlineData(typeof(Exception))]
        public void UnexpectedExceptions_during_write_causes_reply_with_persistencefailure(Type exceptionType)
        {
            var exception = Activator.CreateInstance(exceptionType) as Exception;

            var writer = CreateWriter(writer: MockEventStore.ThrowsOnWrite(exception));

            var request = CreatePersistenceRequest();
            writer.Tell(request);

            ExpectMsg<PersistenceFailure>(msg => msg.PersistenceID == request.PersistenceID && msg.Exception == exception);
        }

        [Fact]
        public void Writer_does_not_publish_to_event_stream()
        {
            var dispatcher = CreateTestProbe();
            var writer = CreateWriter(writer: MockEventStore.SuccessfulWriter(), dispatcher: dispatcher);
            var request = CreatePersistenceRequest();

            var probe = CreateTestProbe();
            Sys.EventStream.Subscribe(probe, typeof(IPersistedEvent));

            writer.Tell(request);

            probe.ExpectNoMsg(TimeSpan.FromMilliseconds(500));
        }
    }
}
