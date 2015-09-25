using Akka.Actor;
using Akka.Event;
using Even.Messages;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics.Contracts;
using System.Threading;
using System.Threading.Tasks;

namespace Even
{
    public class EventStoreReader : ReceiveActor
    {
        Props _readProps;
        Props _readStreamProps;
        Props _readIndexedProjectionProps;

        public EventStoreReader(Props readProps, Props readStreamProps, Props readIndexedProjectionProps)
        {
            _readProps = readProps;
            _readStreamProps = readStreamProps;
            _readIndexedProjectionProps = readIndexedProjectionProps;

            Become(Ready);
        }

        public EventStoreReader()
        {
            Receive<InitializeEventStoreReader>(ini =>
            {
                try
                {
                    _readProps = Props.Create<ReadWorker>(ini.Store, ini.Factory);
                    _readStreamProps = Props.Create<ReadStreamWorker>(ini.Store, ini.Factory);
                    _readIndexedProjectionProps = Props.Create<ReadIndexedProjectionStreamWorker>(ini.Store, ini.Factory);

                    Become(Ready);

                    Sender.Tell(InitializationResult.Successful());
                }
                catch (Exception ex)
                {
                    Sender.Tell(InitializationResult.Failed(ex));
                }
            });
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
