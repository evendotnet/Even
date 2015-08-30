using Akka.Actor;
using Even.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even
{
    /// <summary>
    /// Writes to the projection index.
    /// Since the index is not that important and can be rebuilt at any time,
    /// it writes only at specific intervals in batches to reduce the cost
    /// of writing to the database.
    /// </summary>
    public class ProjectionIndexWriter : ReceiveActor
    {
        public ProjectionIndexWriter()
        {
            Become(Uninitialized);
        }

        IStorageWriter _writer;
        Queue<IProjectionStreamIndex> _buffer = new Queue<IProjectionStreamIndex>();
        bool _flushRequested;

        // TODO: read these from settings
        int _batchSize = 1000;
        TimeSpan _flushTimeout = TimeSpan.FromSeconds(5);

        void Uninitialized()
        {
            Receive<InitializeProjectionIndexWriter>(ini =>
            {
                _writer = ini.WriterFactory();

                Become(ReceivingRequests);
            });
        }

        void ReceivingRequests()
        {
            Receive<PersistProjectionIndexRequest>(request =>
            {
                _buffer.Enqueue(request);

                if (!_flushRequested)
                    RequestFlush();
            });

            Receive<FlushBufferCommand>(_ => FlushBuffer());
        }

        void RequestFlush()
        {
            _flushRequested = true;
            Context.System.Scheduler.ScheduleTellOnce(_flushTimeout, Self, new FlushBufferCommand(), Self);
        }

        async Task FlushBuffer()
        {
            _flushRequested = false;
            
            while (_buffer.Count > 0)
            {
                var list = new List<IProjectionStreamIndex>(_batchSize);

                var remaining = _batchSize;

                while (remaining-- > 0 && _buffer.Count > 0)
                    list.Add(_buffer.Dequeue());

                await _writer.WriteProjectionStreamIndex(list);
            }
        }

        class FlushBufferCommand { }
    }
}
