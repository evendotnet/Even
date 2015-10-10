using Even.Messages;
using Akka.Actor;
using Akka.Actor.Dsl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Even.Tests.Mocks;

namespace Even.Tests
{
    public class AggregateTests : EvenTestKit
    {
        class PersistOne
        {
            public bool Reject { get; set; }
            public bool Throw { get; set; }
        }

        class PersistTwo { }
        class SampleEvent1 { }
        class SampleEvent2 { }

        class Sample : Aggregate
        {
            public Sample()
                : this(null, null)
            { }

            public Sample(IActorRef commandProbe, IActorRef eventProbe)
            {
                commandProbe = commandProbe ?? ActorRefs.Nobody;
                eventProbe = eventProbe ?? ActorRefs.Nobody;

                OnCommand<PersistOne>(c =>
                {
                    if (c.Reject)
                        Reject("Rejected");

                    if (c.Throw)
                        throw new Exception();

                    Persist(new SampleEvent1());

                    commandProbe.Tell(c);
                });

                OnCommand<PersistTwo>(c =>
                {
                    Persist(new SampleEvent1());
                    Persist(new SampleEvent2());

                    commandProbe.Tell(c);
                });

                OnEvent<SampleEvent1>(e =>
                {
                    eventProbe.Tell(e);
                });

                OnEvent<SampleEvent2>(e =>
                {
                    eventProbe.Tell(e);
                });
            }
        }

        static readonly string TestStream = "sample-00000000-0000-0000-0000-000000000000";
        static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(30);

        IActorRef CreateWorkingReader(int eventCount = 0)
        {
            var reader = Sys.ActorOf(conf =>
            {
                conf.Receive<ReadStreamRequest>((r, ctx) =>
                {
                    for (var i = 1; i <= eventCount; i++)
                    {
                        var e = MockPersistedStreamEvent.Create(new SampleEvent1(), i, i, TestStream);
                        ctx.Sender.Tell(new ReadStreamResponse(r.RequestID, e));
                    }

                    ctx.Sender.Tell(new ReadStreamFinished(r.RequestID));
                });
            });

            return reader;
        }

        IActorRef CreateWorkingWriter()
        {
            return Sys.ActorOf(conf =>
            {
                conf.Receive<PersistenceRequest>((r, ctx) => ctx.Sender.Tell(new PersistenceSuccess(r.PersistenceID)));
            });
        }

        IActorRef CreateUnexpectedSequenceWriter()
        {
            return Sys.ActorOf(conf =>
            {
                conf.Receive<PersistenceRequest>((r, ctx) => ctx.Sender.Tell(new UnexpectedStreamSequence(r.PersistenceID)));
            });
        }

        [Fact]
        public void Requests_replay_on_first_valid_command()
        {
            var reader = CreateTestProbe();
            var writer = CreateTestProbe();

            var ag = Sys.ActorOf<Sample>();
            ag.Tell(new InitializeAggregate(reader, writer, new GlobalOptions()));
            ag.Tell(new AggregateCommand(TestStream, new PersistOne(), CommandTimeout));

            reader.ExpectMsg<ReadStreamRequest>(r => r.StreamID == TestStream && r.InitialSequence == 1);
        }

        [Fact]
        public void Does_not_read_events_before_command()
        {
            var reader = CreateTestProbe();
            var writer = CreateTestProbe();

            var ag = Sys.ActorOf<Sample>();
            ag.Tell(new InitializeAggregate(reader, writer, new GlobalOptions()));

            reader.ExpectNoMsg(TimeSpan.FromMilliseconds(500));
        }

        [Fact]
        public void Does_not_read_events_if_command_is_invalid()
        {
            var reader = CreateTestProbe();
            var writer = CreateTestProbe();

            var ag = Sys.ActorOf<Sample>();
            ag.Tell(new InitializeAggregate(reader, writer, new GlobalOptions()));
            ag.Tell(new AggregateCommand("wrong_stream", new PersistOne(), CommandTimeout));

            reader.ExpectNoMsg(TimeSpan.FromMilliseconds(500));
        }

        [Fact]
        public void Persists_one_event()
        {
            var reader = CreateWorkingReader();
            var writer = CreateTestProbe();

            var ag = Sys.ActorOf<Sample>();
            ag.Tell(new InitializeAggregate(reader, writer, new GlobalOptions()));
            ag.Tell(new AggregateCommand(TestStream, new PersistOne(), CommandTimeout));

            writer.ExpectMsg<PersistenceRequest>(r => r.Events.Count == 1);
        }

        [Fact]
        public void Persists_two_events()
        {
            var reader = CreateWorkingReader();
            var writer = CreateTestProbe();

            var ag = Sys.ActorOf<Sample>();
            ag.Tell(new InitializeAggregate(reader, writer, new GlobalOptions()));
            ag.Tell(new AggregateCommand(TestStream, new PersistTwo(), CommandTimeout));

            writer.ExpectMsg<PersistenceRequest>(r => r.Events.Count == 2);
        }

        [Fact]
        public void Multiple_commands_are_processed_in_order()
        {
            var reader = CreateWorkingReader();
            var writer = CreateWorkingWriter();
            var ag = Sys.ActorOf(Props.Create<Sample>(TestActor, null));
            ag.Tell(new InitializeAggregate(reader, writer, new GlobalOptions()));

            var c1 = new PersistOne();
            var c2 = new PersistOne();
            var c3 = new PersistOne();

            ag.Tell(new AggregateCommand(TestStream, c1, CommandTimeout));
            ag.Tell(new AggregateCommand(TestStream, c2, CommandTimeout));
            ag.Tell(new AggregateCommand(TestStream, c3, CommandTimeout));

            ExpectMsg<PersistOne>(c => c == c1);
            ExpectMsg<PersistOne>(c => c == c2);
            ExpectMsg<PersistOne>(c => c == c3);
        }

        [Fact]
        public void Retries_commands_on_unexpected_sequence_before_failing()
        {
            var reader = CreateWorkingReader();
            var writer = CreateUnexpectedSequenceWriter();

            var ag = Sys.ActorOf(Props.Create<Sample>(TestActor, null));
            ag.Tell(new InitializeAggregate(reader, writer, new GlobalOptions { MaxAggregateProcessAttempts = 3 }));
            ag.Tell(new AggregateCommand(TestStream, new PersistOne(), TimeSpan.FromHours(1)), TestActor);

            ExpectMsg<PersistOne>();
            ExpectMsg<PersistOne>();
            ExpectMsg<PersistOne>();
            ExpectMsg<CommandFailed>();
            ExpectNoMsg(TimeSpan.FromMilliseconds(100));
        }

        [Fact]
        public void Rejects_command_on_reject_call()
        {
            var reader = CreateWorkingReader();
            var writer = CreateWorkingWriter();
            var ag = Sys.ActorOf<Sample>();
            ag.Tell(new InitializeAggregate(reader, writer, new GlobalOptions()));
            ag.Tell(new AggregateCommand(TestStream, new PersistOne { Reject = true }, CommandTimeout));

            ExpectMsg<CommandRejected>();
        }

        [Fact]
        public void Fails_command_on_unexpected_error()
        {
            var reader = CreateWorkingReader();
            var writer = CreateWorkingWriter();
            var ag = Sys.ActorOf<Sample>();
            ag.Tell(new InitializeAggregate(reader, writer, new GlobalOptions()));
            ag.Tell(new AggregateCommand(TestStream, new PersistOne { Throw = true }, CommandTimeout));

            ExpectMsg<CommandFailed>();
        }

        [Fact]
        public void Next_command_is_processed_after_failure()
        {
            var reader = CreateWorkingReader();
            var writer = CreateWorkingWriter();
            var ag = Sys.ActorOf(Props.Create<Sample>(TestActor, null));
            ag.Tell(new InitializeAggregate(reader, writer, new GlobalOptions()));

            ag.Tell(new AggregateCommand(TestStream, new PersistOne { Throw = true }, CommandTimeout));
            var expected = new PersistOne();
            ag.Tell(new AggregateCommand(TestStream, expected, CommandTimeout));

            ExpectMsg<CommandFailed>();
            ExpectMsg<PersistOne>(p => p == expected);
        }
        
        [Fact]
        public void Events_are_processed_after_persistence()
        {
            var reader = CreateWorkingReader();
            var writer = CreateWorkingWriter();
            var ag = Sys.ActorOf(Props.Create<Sample>(null, TestActor));
            ag.Tell(new InitializeAggregate(reader, writer, new GlobalOptions()));

            ag.Tell(new AggregateCommand(TestStream, new PersistTwo(), CommandTimeout));

            ExpectMsg<SampleEvent1>();
            ExpectMsg<SampleEvent2>();
        }
    }
}
