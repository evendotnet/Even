using Akka.Actor;
using Even.Messages;
using System;
using System.Collections.Generic;
using Xunit;

namespace Even.Tests
{
    public class SerialEventStreamWriterTests : BaseEventWriterTests<SerialEventStreamWriter>
    { }

    public abstract class BaseEventWriterTests<TActor> : EvenTestKit
        where TActor : ActorBase
    {
        class SampleEvent { }

        protected PersistenceRequest CreatePersistenceRequest(int eventCount = 1)
        {
            return CreatePersistenceRequest(Guid.NewGuid().ToString(), ExpectedSequence.Any, eventCount);
        }

        protected PersistenceRequest CreatePersistenceRequest(string streamId, int expectedSequence, int eventCount)
        {
            var list = new List<UnpersistedEvent>();

            for (var i = 0; i < eventCount; i++)
                list.Add(new UnpersistedEvent(new SampleEvent()));

            return new PersistenceRequest(streamId, expectedSequence, list);
        }

        [Fact]
        public void PersistenceRequest_Must_Reply_Typed_PersistedEvent()
        {
            var props = Props.Create<TActor>(MockStore.Default(), new DefaultSerializer());
            var writer = Sys.ActorOf(props);

            var request = CreatePersistenceRequest();
            writer.Tell(request);

            ExpectMsg<IPersistedEvent<SampleEvent>>();
        }

        [Fact]
        public void PersistenceRequest_Must_Reply_PersistenceSuccess()
        {
            var props = Props.Create<TActor>(MockStore.Default(), new MockSerializer());
            var writer = Sys.ActorOf(props);

            var request = CreatePersistenceRequest();
            writer.Tell(request);

            ExpectMsgEventually<PersistenceSuccess>(m => m.PersistenceID == request.PersistenceID);
        }

        [Fact]
        public void UnexpectedStreamSequenceException_Must_Reply_UnexpectedStreamSequence_Message()
        {
            var props = Props.Create<TActor>(MockStore.ThrowsOnWrite<UnexpectedStreamSequenceException>(), new MockSerializer());
            var writer = Sys.ActorOf(props);

            var request = CreatePersistenceRequest();
            writer.Tell(request);

            ExpectMsg<UnexpectedStreamSequence>(msg => msg.PersistenceID == request.PersistenceID);
        }

        [Fact]
        public void DuplicatedEventException_Must_Reply_DuplicatedEvent_Message()
        {
            var props = Props.Create<TActor>(MockStore.ThrowsOnWrite<DuplicatedEventException>(), new MockSerializer());
            var writer = Sys.ActorOf(props);

            var request = CreatePersistenceRequest();
            writer.Tell(request);

            ExpectMsg<DuplicatedEvent>(msg => msg.PersistenceID == request.PersistenceID);
        }

        [Theory]
        [InlineData(typeof(ArgumentException))]
        [InlineData(typeof(TimeoutException))]
        [InlineData(typeof(Exception))]
        public void UnexpectedExceptions_During_Write_Must_Reply_With_PersistenceFailure(Type exceptionType)
        {
            var exception = Activator.CreateInstance(exceptionType) as Exception;

            var props = PropsFactory.Create<TActor>(MockStore.ThrowsOnWrite(exception), new MockSerializer());
            var writer = Sys.ActorOf(props);

            var request = CreatePersistenceRequest();
            writer.Tell(request);

            ExpectMsg<PersistenceFailure>(msg => msg.PersistenceID == request.PersistenceID && msg.Exception == exception);
        }
    }
}
