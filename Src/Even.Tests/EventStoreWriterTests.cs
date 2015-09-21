using Even.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Akka.Actor;

namespace Even.Tests
{
    public class EventStoreWriterTests : EvenTestKit
    {
        #region Helpers

        static readonly TimeSpan NoMsgTimeout = TimeSpan.FromMilliseconds(100);

        void Initialize(IActorRef writer)
        {
            writer.Tell(new InitializeEventStoreWriter
            {
                Dispatcher = CreateTestProbe(),
                Serializer = new MockSerializer(),
                StoreWriter = Mocks.MockEventStore.SuccessfulWriter(),
            });

            ExpectMsg<InitializationResult>(r => r.Initialized);
        }

        #endregion

        [Fact]
        public void Initialization_replies_with_initialized_message()
        {
            var probe = CreateTestProbe();

            var actor = Sys.ActorOf<EventStoreWriter>();

            actor.Tell(new InitializeEventStoreWriter
            {
                Dispatcher = CreateTestProbe(),
                Serializer = new MockSerializer(),
                StoreWriter = Mocks.MockEventStore.SuccessfulWriter(),
            });

            ExpectMsg<InitializationResult>(r => r.Initialized);
        }

        [Fact]
        public void Requests_with_expected_version_are_forwarded_to_serial_writer_only()
        {
            var s = CreateTestProbe();
            var b = CreateTestProbe();
            var i = CreateTestProbe();
            var c = CreateTestProbe();

            var props = Props.Create<EventStoreWriter>(s, b, i, c);
            var writer = Sys.ActorOf(props);
            Initialize(writer);
            
            writer.Tell(new PersistenceRequest("a", 0, new[] { new UnpersistedEvent("a", new object()) }));

            s.ExpectMsg<PersistenceRequest>();

            b.ExpectNoMsg(NoMsgTimeout);
            i.ExpectNoMsg(NoMsgTimeout);
            c.ExpectNoMsg(NoMsgTimeout);
        }

        [Fact]
        public void Requests_with_any_version_are_forwarded_to_buffered_writer_only()
        {
            var s = CreateTestProbe();
            var b = CreateTestProbe();
            var i = CreateTestProbe();
            var c = CreateTestProbe();

            var props = Props.Create<EventStoreWriter>(s, b, i, c);
            var writer = Sys.ActorOf(props);
            Initialize(writer);

            writer.Tell(new PersistenceRequest("a", ExpectedSequence.Any, new[] { new UnpersistedEvent("a", new object()) }));

            b.ExpectMsg<PersistenceRequest>();

            s.ExpectNoMsg(NoMsgTimeout);
            i.ExpectNoMsg(NoMsgTimeout);
            c.ExpectNoMsg(NoMsgTimeout);
        }

        [Fact]
        public void Requests_for_multiple_streams_are_forwarded_to_buffered_writer_only()
        {
            var s = CreateTestProbe();
            var b = CreateTestProbe();
            var i = CreateTestProbe();
            var c = CreateTestProbe();

            var props = Props.Create<EventStoreWriter>(s, b, i, c);
            var writer = Sys.ActorOf(props);
            Initialize(writer);

            writer.Tell(new PersistenceRequest(new[]
            {
                new UnpersistedEvent("a", new object()),
                new UnpersistedEvent("b", new object())
            }));

            b.ExpectMsg<PersistenceRequest>();

            s.ExpectNoMsg(NoMsgTimeout);
            i.ExpectNoMsg(NoMsgTimeout);
            c.ExpectNoMsg(NoMsgTimeout);
        }

        [Fact]
        public void Requests_for_index_writes_are_forwarded_to_index_writer_only()
        {
            var s = CreateTestProbe();
            var b = CreateTestProbe();
            var i = CreateTestProbe();
            var c = CreateTestProbe();

            var props = Props.Create<EventStoreWriter>(s, b, i, c);
            var writer = Sys.ActorOf(props);
            Initialize(writer);

            writer.Tell(new ProjectionIndexPersistenceRequest("a", 1, 1));

            i.ExpectMsg<ProjectionIndexPersistenceRequest>();

            b.ExpectNoMsg(NoMsgTimeout);
            s.ExpectNoMsg(NoMsgTimeout);
            c.ExpectNoMsg(NoMsgTimeout);
        }

        [Fact]
        public void Request_for_checkpoint_writes_are_forwarded_to_checkpoint_writer_only()
        {
            var s = CreateTestProbe();
            var b = CreateTestProbe();
            var i = CreateTestProbe();
            var c = CreateTestProbe();

            var props = Props.Create<EventStoreWriter>(s, b, i, c);
            var writer = Sys.ActorOf(props);
            Initialize(writer);

            writer.Tell(new ProjectionCheckpointPersistenceRequest("a", 1));

            c.ExpectMsg<ProjectionCheckpointPersistenceRequest>();

            b.ExpectNoMsg(NoMsgTimeout);
            s.ExpectNoMsg(NoMsgTimeout);
            i.ExpectNoMsg(NoMsgTimeout);
        }
    }
}
