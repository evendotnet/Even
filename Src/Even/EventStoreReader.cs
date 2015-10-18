using Akka.Actor;
using Even.Messages;
using System.Threading.Tasks;

namespace Even
{
    public class EventStoreReader : ReceiveActor
    {
        Props _readProps;
        Props _readStreamProps;
        Props _readIndexedProjectionProps;
        Props _readProjectionCheckpointProps;
        Props _readHighestGlobalSequenceProps;

        public static Props CreateProps(IEventStore store, IPersistedEventFactory factory, GlobalOptions options)
        {
            return Props.Create<EventStoreReader>(store, factory, options);
        }

        public EventStoreReader(IEventStore store, IPersistedEventFactory factory, GlobalOptions options)
        {
            _readProps = ReadWorker.CreateProps(store, factory);
            _readStreamProps = ReadStreamWorker.CreateProps(store, factory);
            _readIndexedProjectionProps = ReadIndexedProjectionStreamWorker.CreateProps(store, factory);
            _readProjectionCheckpointProps = ReadProjectionCheckpointWorker.CreateProps(store);
            _readHighestGlobalSequenceProps = ReadHighestGlobalSequenceWorker.CreateProps(store);

            Ready();
        }

        // test only
        public static Props CreateProps(Props readProps, Props readStreamProps, Props readIndexedProjectionProps, Props readProjectionCheckpointProps, Props readHighestGlobalSequenceProps, GlobalOptions options)
        {
            return Props.Create<EventStoreReader>(readProps, readStreamProps, readIndexedProjectionProps, readProjectionCheckpointProps, readHighestGlobalSequenceProps, options);
        }

        // test only
        public EventStoreReader(Props readProps, Props readStreamProps, Props readIndexedProjectionProps, Props readProjectionCheckpointProps, Props readHighestGlobalSequenceProps, GlobalOptions options)
        {
            _readProps = readProps;
            _readStreamProps = readStreamProps;
            _readIndexedProjectionProps = readIndexedProjectionProps;
            _readProjectionCheckpointProps = readProjectionCheckpointProps;
            _readHighestGlobalSequenceProps = readHighestGlobalSequenceProps;

            Ready();
        }

        void Ready()
        {
            Receive<ReadRequest>(r =>
            {
                var worker = Context.ActorOf(_readProps);
                worker.Forward(r);
            });

            Receive<ReadStreamRequest>(r =>
            {
                var worker = Context.ActorOf(_readStreamProps);
                worker.Forward(r);
            });

            Receive<ReadIndexedProjectionStreamRequest>(r =>
            {
                var worker = Context.ActorOf(_readIndexedProjectionProps);
                worker.Forward(r);
            });

            Receive<ReadProjectionCheckpointRequest>(r =>
            {
                var worker = Context.ActorOf(_readProjectionCheckpointProps);
                worker.Forward(r);
            });

            Receive<ReadHighestGlobalSequenceRequest>(r =>
            {
                var worker = Context.ActorOf(_readHighestGlobalSequenceProps);
                worker.Forward(r);
            });
        }
    }
}
