using Akka.Actor;
using Akka.Event;
using Even.Messages;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Threading.Tasks;

namespace Even
{
    public class EventStoreWriter : ReceiveActor
    {
        IEventStoreWriter _writer;
        ISerializer _serializer;

        IActorRef _eventWriter;
        IActorRef _indexWriter;

        public EventStoreWriter()
        {
            Receive<InitializeEventStoreWriter>(ini =>
            {
                _writer = ini.StoreWriter;
                _serializer = ini.Serializer;

                var ewProps = PropsFactory.Create<SerialEventStreamWriter>(_writer, _serializer);
                _eventWriter = Context.ActorOf(ewProps, "eventwriter");

                // initialize projection index writer
                var pWriter = _writer as IProjectionStoreWriter;

                if (pWriter != null)
                {
                    var pwProps = PropsFactory.Create<ProjectionIndexWriter>(pWriter);
                    _indexWriter = Context.ActorOf(pwProps, "projectionwriter");
                }

                Become(Ready);
            });
        }

        public void Ready()
        {
            Receive<PersistenceRequest>(request =>
            {
                _eventWriter.Forward(request);
            });

            Receive<ProjectionIndexPersistenceRequest>(request =>
            {
                if (_indexWriter != null)
                    _indexWriter.Forward(request);
            });
        }
    }
   

    public class ProjectionIndexWriter : ReceiveActor
    {
        IProjectionStoreWriter _writer;
        LinkedList<ProjectionIndexPersistenceRequest> _buffer = new LinkedList<ProjectionIndexPersistenceRequest>();
        bool _flushRequested;

        TimeSpan _flushDelay = TimeSpan.FromSeconds(5);

        public ProjectionIndexWriter(IProjectionStoreWriter writer)
        {
            _writer = writer;

            Receive<ProjectionIndexPersistenceRequest>(request => AddToBuffer(request));
            Receive<WriteBufferCommand>(_ => WriteBuffer());
        }

        void AddToBuffer(ProjectionIndexPersistenceRequest request)
        {
            _buffer.AddLast(request);

            if (_buffer.Count > 500)
                Self.Tell(new WriteBufferCommand());

            else if (!_flushRequested)
            {
                _flushRequested = true;
                Context.System.Scheduler.ScheduleTellOnce(_flushDelay, Self, new WriteBufferCommand(), Self);
            }
        }

        async Task WriteBuffer()
        {
            _flushRequested = false;

            var re = from e in _buffer
                     group e by e.ProjectionStreamID into g
                     select new
                     {
                         ProjectionStreamID = g.Key,
                         Entries = g.Select(o => o.GlobalSequence).ToList()
                     };
            // TODO: take into account the projection sequence
            //foreach (var o in re)
            //    await _writer.WriteProjectionIndexAsync(o.ProjectionStreamID, o.Entries);

            _buffer.Clear();
        }

        class WriteBufferCommand { }
    }
}
