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

        IActorRef CreateDispatcher(int initialGlobalSequence = 0, TimeSpan? recoveryStartTimeout = null)
        {
            var reader = CreateTestReader(initialGlobalSequence);
            recoveryStartTimeout = recoveryStartTimeout ?? TimeSpan.FromMilliseconds(100);

            var dispatcher = Sys.ActorOf<EventDispatcher>();
            dispatcher.Tell(new InitializeEventDispatcher { Reader = reader, RecoveryStartTimeout = recoveryStartTimeout.Value });

            return dispatcher;
        }

        static IPersistedStreamEvent CreateTestEvent(long globalSequence)
        {
            var e = Substitute.For<IPersistedStreamEvent>();
            e.GlobalSequence.Returns(globalSequence);
            return e;
        }

        TestProbe CreateTestReader(int initialGlobalSequence = 0)
        {
            var reader = CreateTestProbe();

            reader.SetAutoPilot(new DelegateAutoPilot((IActorRef sender, object msg) =>
            {
                if (msg is GlobalSequenceRequest)
                {
                    var r = (GlobalSequenceRequest)msg;
                    sender.Tell(new GlobalSequenceResponse { ReplayID = r.ReplayID, LastGlobalSequence = initialGlobalSequence });
                }

                if (msg is EventReplayRequest)
                {
                    var r = (EventReplayRequest)msg;
                    var start = r.InitialGlobalSequence;
                    var last = start + r.Count - 1;

                    for (var i = start; i <= last; i++)
                        sender.Tell(new ReplayEvent { ReplayID = r.ReplayID, Event = CreateTestEvent(i) });

                    sender.Tell(new ReplayCompleted { ReplayID = r.ReplayID, LastSeenGlobalSequence = last });
                }

                return AutoPilot.KeepRunning;
            }));

            return reader;
        }
       
        #endregion

        [Fact]
        public void Normal_initialization_message_replies_as_initialized_and_request_global_sequence_to_reader()
        {
            var dispatcher = Sys.ActorOf<EventDispatcher>();

            var reader = CreateTestProbe();
            dispatcher.Tell(new InitializeEventDispatcher { Reader = reader });

            ExpectMsg<InitializationResult>(i => i.Initialized);
            reader.ExpectMsg<GlobalSequenceRequest>();
        }

        [Fact]
        public void Dispatcher_publishes_events_to_eventstream()
        {
            var dispatcher = CreateDispatcher();
            
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
        public void Gaps_in_sequence_causes_dispatcher_to_get_events_from_reader()
        {
            var dispatcher = CreateDispatcher();

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
            var dispatcher = Sys.ActorOf<EventDispatcher>();
            var reader = CreateTestReader(0);
            dispatcher.Tell(new InitializeEventDispatcher { Reader = reader, RecoveryStartTimeout = TimeSpan.FromMilliseconds(100) });

            reader.ExpectMsg<GlobalSequenceRequest>();

            foreach (var i in new[] { 1, 4, 5, 9, 20, 22 })
                dispatcher.Tell(CreateTestEvent(i));

            reader.ExpectMsg<EventReplayRequest>(r => r.InitialGlobalSequence == 2 && r.Count == 2);
            reader.ExpectMsg<EventReplayRequest>(r => r.InitialGlobalSequence == 6 && r.Count == 3);
            reader.ExpectMsg<EventReplayRequest>(r => r.InitialGlobalSequence == 10 && r.Count == 10);
            reader.ExpectMsg<EventReplayRequest>(r => r.InitialGlobalSequence == 21 && r.Count == 1);
        }


    }
}
