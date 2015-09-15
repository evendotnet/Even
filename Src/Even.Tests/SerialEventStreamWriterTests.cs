using Akka.Actor;
using Even.Messages;
using System;
using System.Collections.Generic;
using Xunit;

namespace Even.Tests
{
    public class SerialEventStreamWriterTests : EvenTestKit
    {
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
                list.Add(new UnpersistedEvent(new SampleEvent1()));

            return new PersistenceRequest(streamId, expectedSequence, list);
        }

        [Fact]
        public void Writer_Replies_PersistedEvents_In_Request_Order()
        {
            var props = Props.Create<SerialEventStreamWriter>(MockStore.Default(), new DefaultSerializer());
            var writer = Sys.ActorOf(props);

            var request = new PersistenceRequest("a", ExpectedSequence.Any, new[] {
                new UnpersistedEvent(new SampleEvent3()),
                new UnpersistedEvent(new SampleEvent1()),
                new UnpersistedEvent(new SampleEvent2())
            });

            writer.Tell(request);

            ExpectMsg<IPersistedEvent<SampleEvent3>>();
            ExpectMsg<IPersistedEvent<SampleEvent1>>();
            ExpectMsg<IPersistedEvent<SampleEvent2>>();
            ExpectMsg<PersistenceSuccess>();
        }

        [Fact]
        public void Writer_Publishes_PersistedEvents_To_EventStream_In_Request_Order()
        {
            var props = Props.Create<SerialEventStreamWriter>(MockStore.Default(), new DefaultSerializer());
            var writer = Sys.ActorOf(props);

            var request = new PersistenceRequest("a", ExpectedSequence.Any, new[] {
                new UnpersistedEvent(new SampleEvent3()),
                new UnpersistedEvent(new SampleEvent1()),
                new UnpersistedEvent(new SampleEvent2())
            });

            var probe = CreateTestProbe();
            Sys.EventStream.Subscribe(probe, typeof(IPersistedEvent));

            writer.Tell(request);

            probe.ExpectMsg<IPersistedEvent<SampleEvent3>>();
            probe.ExpectMsg<IPersistedEvent<SampleEvent1>>();
            probe.ExpectMsg<IPersistedEvent<SampleEvent2>>();
            probe.ExpectNoMsg(50);
        }

        [Fact]
        public void Writer_Must_Reply_Typed_PersistedEvent_Then_PersistenceSuccess()
        {
            var props = Props.Create<SerialEventStreamWriter>(MockStore.Default(), new DefaultSerializer());
            var writer = Sys.ActorOf(props);

            var request = CreatePersistenceRequest();
            writer.Tell(request);

            ExpectMsg<IPersistedEvent<SampleEvent1>>();
            ExpectMsg<PersistenceSuccess>();
        }

        [Fact]
        public void UnexpectedStreamSequenceException_Causes_UnexpectedStreamSequence_Message()
        {
            var props = Props.Create<SerialEventStreamWriter>(MockStore.ThrowsOnWrite<UnexpectedStreamSequenceException>(), new MockSerializer());
            var writer = Sys.ActorOf(props);

            var request = CreatePersistenceRequest();
            writer.Tell(request);

            ExpectMsg<UnexpectedStreamSequence>(msg => msg.PersistenceID == request.PersistenceID);
        }

        [Fact]
        public void DuplicatedEventException_Causes_DuplicatedEvent_Message()
        {
            var props = Props.Create<SerialEventStreamWriter>(MockStore.ThrowsOnWrite<DuplicatedEventException>(), new MockSerializer());
            var writer = Sys.ActorOf(props);

            var request = CreatePersistenceRequest();
            writer.Tell(request);

            ExpectMsg<DuplicatedEvent>(msg => msg.PersistenceID == request.PersistenceID);
        }

        [Theory]
        [InlineData(typeof(ArgumentException))]
        [InlineData(typeof(TimeoutException))]
        [InlineData(typeof(Exception))]
        public void UnexpectedExceptions_During_Write_Causes_Reply_With_PersistenceFailure(Type exceptionType)
        {
            var exception = Activator.CreateInstance(exceptionType) as Exception;

            var props = PropsFactory.Create<SerialEventStreamWriter>(MockStore.ThrowsOnWrite(exception), new MockSerializer());
            var writer = Sys.ActorOf(props);

            var request = CreatePersistenceRequest();
            writer.Tell(request);

            ExpectMsg<PersistenceFailure>(msg => msg.PersistenceID == request.PersistenceID && msg.Exception == exception);
        }
    }
}
