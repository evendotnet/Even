using Akka.Actor;
using Akka.TestKit;
using Even.Messages;
using NSubstitute;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Even.Tests
{
    public class EventDispatcherTests : EvenTestKit
    {
        #region Helpers

        IActorRef CreateDispatcher(int initialGlobalSequence, TimeSpan recoveryStartTimeout)
        {
            var reader = CreateTestReader(initialGlobalSequence);
            var dispatcher = Sys.ActorOf(EventDispatcher.CreateProps(reader, new GlobalOptions { DispatcherRecoveryTimeout = recoveryStartTimeout }));

            return dispatcher;
        }

        static IPersistedEvent CreateTestEvent(long globalSequence)
        {
            var e = Substitute.For<IPersistedEvent>();
            e.GlobalSequence.Returns(globalSequence);
            return e;
        }

        TestProbe CreateTestReader(int initialGlobalSequence = 0)
        {
            var reader = CreateTestProbe();

            reader.SetAutoPilot(new DelegateAutoPilot((IActorRef sender, object msg) =>
            {
                if (msg is ReadHighestGlobalSequenceRequest)
                {
                    var r = (ReadHighestGlobalSequenceRequest)msg;
                    sender.Tell(new ReadHighestGlobalSequenceResponse(r.RequestID, initialGlobalSequence));
                }

                if (msg is ReadRequest)
                {
                    var r = (ReadRequest)msg;
                    var start = r.InitialGlobalSequence;
                    var last = start + r.Count - 1;

                    for (var i = start; i <= last; i++)
                        sender.Tell(new ReadResponse(r.RequestID, CreateTestEvent(i)));

                    sender.Tell(new ReadFinished(r.RequestID));
                }

                return AutoPilot.KeepRunning;
            }));

            return reader;
        }

        #endregion

        [Fact]
        public void Normal_initialization_message_replies_as_initialized_and_request_global_sequence_to_reader()
        {
            var reader = CreateTestProbe();
            var dispatcher = Sys.ActorOf(EventDispatcher.CreateProps(reader, new GlobalOptions()));

            reader.ExpectMsg<ReadHighestGlobalSequenceRequest>();
        }

        [Fact]
        public void Dispatcher_publishes_events_to_eventstream()
        {
            var dispatcher = CreateDispatcher(0, TimeSpan.FromSeconds(1));

            var probe = CreateTestProbe();
            Sys.EventStream.Subscribe(probe, typeof(IPersistedEvent));

            var event1 = CreateTestEvent(1);
            var event2 = CreateTestEvent(2);
            var event3 = CreateTestEvent(3);

            dispatcher.Tell(event1);
            dispatcher.Tell(event2);
            dispatcher.Tell(event3);

            probe.ExpectMsg<IPersistedEvent>(e => e == event1);
            probe.ExpectMsg<IPersistedEvent>(e => e == event2);
            probe.ExpectMsg<IPersistedEvent>(e => e == event3);
        }

        [Fact]
        public void Gaps_in_sequence_causes_dispatcher_to_get_missing_events_from_reader()
        {
            var dispatcher = CreateDispatcher(0, TimeSpan.FromMilliseconds(100));

            var probe = CreateTestProbe();
            Sys.EventStream.Subscribe(probe, typeof(IPersistedEvent));

            var event1 = CreateTestEvent(1);
            var event4 = CreateTestEvent(4);

            dispatcher.Tell(event1);
            dispatcher.Tell(event4);

            probe.ExpectMsg<IPersistedEvent>(e => e.GlobalSequence == 1);
            probe.ExpectMsg<IPersistedEvent>(e => e.GlobalSequence == 2);
            probe.ExpectMsg<IPersistedEvent>(e => e.GlobalSequence == 3);
            probe.ExpectMsg<IPersistedEvent>(e => e.GlobalSequence == 4);
        }

        [Fact]
        public void Dispatcher_requests_correct_missing_events_to_reader()
        {
            var reader = CreateTestReader(0);
            var dispatcher = Sys.ActorOf(EventDispatcher.CreateProps(reader, new GlobalOptions { DispatcherRecoveryTimeout = TimeSpan.FromMilliseconds(100) }));

            reader.ExpectMsg<ReadHighestGlobalSequenceRequest>();

            foreach (var i in new[] { 1, 4, 5, 9, 20, 22 })
                dispatcher.Tell(CreateTestEvent(i));

            reader.ExpectMsg<ReadRequest>(r => r.InitialGlobalSequence == 2 && r.Count == 2);
            reader.ExpectMsg<ReadRequest>(r => r.InitialGlobalSequence == 6 && r.Count == 3);
            reader.ExpectMsg<ReadRequest>(r => r.InitialGlobalSequence == 10 && r.Count == 10);
            reader.ExpectMsg<ReadRequest>(r => r.InitialGlobalSequence == 21 && r.Count == 1);
        }
    }
}
