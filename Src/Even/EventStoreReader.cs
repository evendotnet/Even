using Akka.Actor;
using Even.Messages;
using System.Threading.Tasks;

namespace Even
{
    public class EventStoreReader : ReceiveActor
    {
        IEventStoreReader _reader;

        Props _readProps;
        Props _readStreamProps;
        Props _readIndexedProjectionProps;
        Props _readHighestGlobalSequenceProps;

        public static Props CreateProps(IEventStoreReader storeReader, IPersistedEventFactory factory, GlobalOptions options)
        {
            return Props.Create<EventStoreReader>(storeReader, factory, options);
        }

        public EventStoreReader(IEventStoreReader storeReader, IPersistedEventFactory factory, GlobalOptions options)
        {
            _reader = storeReader;

            _readProps = ReadWorker.CreateProps(storeReader, factory);
            _readStreamProps = ReadStreamWorker.CreateProps(storeReader, factory);
            _readIndexedProjectionProps = ReadIndexedProjectionStreamWorker.CreateProps(storeReader, factory);
            _readHighestGlobalSequenceProps = ReadHighestGlobalSequenceWorker.CreateProps(storeReader);

            Ready();
        }

        // test only
        public static Props CreateProps(Props readProps, Props readStreamProps, Props readIndexedProjectionProps, Props readHighestGlobalSequenceProps, GlobalOptions options)
        {
            return Props.Create<EventStoreReader>(readProps, readStreamProps, readIndexedProjectionProps, readHighestGlobalSequenceProps,  options);
        }

        // test only
        public EventStoreReader(Props readProps, Props readStreamProps, Props readIndexedProjectionProps, Props readHighestGlobalSequenceProps, GlobalOptions options)
        {
            _readProps = readProps;
            _readStreamProps = readStreamProps;
            _readIndexedProjectionProps = readIndexedProjectionProps;
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

            Receive<ReadHighestGlobalSequenceRequest>(r =>
            {
                var worker = Context.ActorOf(_readHighestGlobalSequenceProps);
                worker.Forward(r);
            });
        }
    }
}
