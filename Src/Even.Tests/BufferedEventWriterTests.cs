using Akka.Actor;
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
    public class BufferedEventWriterTests : EvenTestKit
    {
        #region Helpers

        class SampleEvent1 { }
        class SampleEvent2 { }
        class SampleEvent3 { }

        protected PersistenceRequest CreatePersistenceRequest(int eventCount = 1)
        {
            return CreatePersistenceRequest(Guid.NewGuid().ToString(), ExpectedSequence.Any, eventCount);
        }

        protected PersistenceRequest CreatePersistenceRequest(string streamName, int expectedSequence, int eventCount)
        {
            var list = new List<UnpersistedEvent>();

            for (var i = 0; i < eventCount; i++)
                list.Add(new UnpersistedEvent(streamName, new SampleEvent1()));

            return new PersistenceRequest(list);
        }

        protected IActorRef CreateWriter(IEventStoreWriter writer = null, ISerializer serializer = null, IActorRef dispatcher = null)
        {
            writer = writer ?? MockEventStore.SuccessfulWriter();
            serializer = serializer ?? new MockSerializer();
            dispatcher = dispatcher ?? CreateTestProbe();

            var props = BufferedEventWriter.CreateProps(writer, serializer, dispatcher, new GlobalOptions());
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
        public void DuplicatedEventException_causes_duplicatedevent_message()
        {
            var writer = CreateWriter(writer: MockEventStore.ThrowsOnWrite<DuplicatedEntryException>());
            var request = CreatePersistenceRequest();
            writer.Tell(request);

            ExpectMsg<DuplicatedEntry>(msg => msg.PersistenceID == request.PersistenceID, TimeSpan.FromMinutes(5));
        }

        [Fact]
        public void Exception_for_some_items_still_writes_the_others()
        {
            // the writer will fail on the batch and on the next 3 requests
            var writer = CreateWriter(writer: MockEventStore.ThrowsOnWrite(new Exception(), new[] { 1, 2, 3, 4 }));

            var requests = Enumerable.Range(0, 100).Select(_ => CreatePersistenceRequest(1)).ToList();

            foreach (var r in requests)
                writer.Tell(r);

            var list = new List<object>();

            for (var i = 0; i < requests.Count; i++)
            {
                var request = requests[i];

                if (i <= 2)
                    ExpectMsg<PersistenceFailure>(msg => msg.PersistenceID == request.PersistenceID);
                else
                    ExpectMsg<PersistenceSuccess>(msg => msg.PersistenceID == request.PersistenceID);
            }
        }

        [Fact] 
        public void Writer_does_not_publish_to_event_stream()
        {
            var dispatcher = CreateTestProbe();
            var writer = CreateWriter(writer: MockEventStore.SuccessfulWriter(), dispatcher: dispatcher);

            var request = new PersistenceRequest(new[] {
                new UnpersistedEvent("a", new SampleEvent3()),
                new UnpersistedEvent("a", new SampleEvent1()),
                new UnpersistedEvent("a", new SampleEvent2())
            });

            var probe = CreateTestProbe();
            Sys.EventStream.Subscribe(probe, typeof(IPersistedEvent));

            writer.Tell(request);

            probe.ExpectNoMsg(TimeSpan.FromMilliseconds(500));
        }
    }
}
