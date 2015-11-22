using Akka.Actor;
using Akka.Event;
using Even.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even
{
    public class ProjectionCheckpointWriter : ReceiveActor
    {
        IProjectionStoreWriter _writer;
        GlobalOptions _options;

        LinkedList<ProjectionCheckpointPersistenceRequest> _buffer = new LinkedList<ProjectionCheckpointPersistenceRequest>();
        bool _flushRequested;

        public static Props CreateProps(IProjectionStoreWriter writer, GlobalOptions options)
        {
            Argument.RequiresNotNull(writer, nameof(writer));
            Argument.RequiresNotNull(options, nameof(options));

            return Props.Create<ProjectionCheckpointWriter>(writer, options);
        }

        public ProjectionCheckpointWriter(IProjectionStoreWriter writer, GlobalOptions options)
        {
            _writer = writer;
            _options = options;

            Receive<ProjectionCheckpointPersistenceRequest>(request => Enqueue(request));
            Receive<FlushBufferCommand>(_ => FlushBuffer());
        }

        void Enqueue(ProjectionCheckpointPersistenceRequest request)
        {
            _buffer.AddLast(request);

            if (!_flushRequested)
            {
                _flushRequested = true;
                Context.System.Scheduler.ScheduleTellOnce(_options.CheckpointWriterFlushDelay, Self, new FlushBufferCommand(), Self);
            }
        }

        async Task FlushBuffer()
        {
            _flushRequested = false;

            var re = from e in _buffer
                     group e by e.Stream into g
                     select new { Stream = g.Key, GlobalSequence = g.Max(o => o.GlobalSequence) };

            foreach (var o in re)
            {
                try
                {
                    await _writer.WriteProjectionCheckpointAsync(o.Stream, o.GlobalSequence);
                }
                catch (Exception ex)
                {
                    Context.GetLogger().Error(ex, "Error writing projection checkpoint.");
                }
            }

            _buffer.Clear();
        }

        class FlushBufferCommand { }
    }
}
