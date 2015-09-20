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
    public class EventDispatcherBasicTests : EvenTestKit
    {
        #region Helpers

        IActorRef CreateDispatcher()
        {
            var actor = Sys.ActorOf<EventDispatcher>();
            return actor;
        }

        IActorRef CreateAndInitializeDispatcher(TestProbe reader = null, TimeSpan? recoveryStartTimeout = null)
        {
            reader = reader ?? CreateTestProbe();
            recoveryStartTimeout = recoveryStartTimeout ?? TimeSpan.FromMilliseconds(100);

            var dispatcher = CreateDispatcher();
            dispatcher.Tell(new InitializeEventDispatcher { Reader = reader, RecoveryStartTimeout = recoveryStartTimeout.Value });

            ExpectMsg<InitializationResult>(msg => msg.Initialized);
            var request = reader.ExpectMsg<GlobalSequenceRequest>();
            dispatcher.Tell(new GlobalSequenceResponse { ReplayID = request.ReplayID, LastGlobalSequence = 0 }, reader);

            return dispatcher;
        }

        IPersistedStreamEvent CreateTestEvent(long globalSequence)
        {
            var e = Substitute.For<IPersistedStreamEvent>();
            e.GlobalSequence.Returns(globalSequence);
            return e;
        }

        #endregion

        [Fact]
        public void Normal_initialization_message_replies_as_initialized_and_request_global_sequence()
        {
            CreateAndInitializeDispatcher();
        }

        [Fact]
        public void Dispatcher_emits_to_event_stream()
        {
            var dispatcher = CreateAndInitializeDispatcher();
            
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
        public void Missing_sequence_causes_dispatcher_to_request_event_to_reader()
        {
            var reader = CreateTestProbe();
            var dispatcher = CreateAndInitializeDispatcher(reader);

            var probe = CreateTestProbe();
            Sys.EventStream.Subscribe(probe, typeof(IPersistedEvent));

            var event1 = CreateTestEvent(1);
            var event3 = CreateTestEvent(3);

            dispatcher.Tell(event1);
            dispatcher.Tell(event3);

            reader.ExpectMsg<EventReplayRequest>(msg => msg.InitialGlobalSequence == 2 && msg.Count == 1);
        }
    }
}
