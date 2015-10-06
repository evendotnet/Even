using Akka.Actor;
using Even.Messages;

namespace Even
{
    public class EventStoreReader : ReceiveActor
    {
        Props _readProps;
        Props _readStreamProps;
        Props _readIndexedProjectionProps;

        public static Props CreateProps(IEventStoreReader storeReader, IPersistedEventFactory factory, GlobalOptions options)
        {
            return Props.Create<EventStoreReader>(storeReader, factory, options);
        }

        public EventStoreReader(IEventStoreReader storeReader, IPersistedEventFactory factory, GlobalOptions options)
        {
            _readProps = Props.Create<ReadWorker>(storeReader, factory);
            _readStreamProps = Props.Create<ReadStreamWorker>(storeReader, factory);
            _readIndexedProjectionProps = Props.Create<ReadIndexedProjectionStreamWorker>(storeReader, factory);

            Ready();
        }

        // test only
        public static Props CreateProps(Props readProps, Props readStreamProps, Props readIndexedProjectionProps, GlobalOptions options)
        {
            return Props.Create<EventStoreReader>(readProps, readStreamProps, readIndexedProjectionProps, options);
        }

        // test only
        public EventStoreReader(Props readProps, Props readStreamProps, Props readIndexedProjectionProps, GlobalOptions options)
        {
            _readProps = readProps;
            _readStreamProps = readStreamProps;
            _readIndexedProjectionProps = readIndexedProjectionProps;

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
        }
    }
}
