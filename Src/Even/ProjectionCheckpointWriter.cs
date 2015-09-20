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
        LinkedList<ProjectionCheckpointPersistenceRequest> _buffer = new LinkedList<ProjectionCheckpointPersistenceRequest>();
        TimeSpan _flushDelay;
        bool _flushRequested;

        public ProjectionCheckpointWriter(IProjectionStoreWriter writer, TimeSpan flushDelay)
        {
            Argument.Requires(writer != null);
            Argument.Requires(flushDelay < TimeSpan.FromSeconds(30), "Flush delay shouldn't be too high.");

            _writer = writer;
            _flushDelay = flushDelay;

            Receive<ProjectionCheckpointPersistenceRequest>(request => Enqueue(request));
            Receive<FlushBufferCommand>(_ => FlushBuffer());
        }

        void Enqueue(ProjectionCheckpointPersistenceRequest request)
        {
            _buffer.AddLast(request);

            if (!_flushRequested)
            {
                _flushRequested = true;
                Context.System.Scheduler.ScheduleTellOnce(_flushDelay, Self, new FlushBufferCommand(), Self);
            }
        }

        async Task FlushBuffer()
        {
            _flushRequested = false;

            var re = from e in _buffer
                     group e by e.StreamID into g
                     select new { StreamID = g.Key, GlobalSequence = g.Max(o => o.GlobalSequence) };

            foreach (var o in re)
            {
                try
                {
                    await _writer.WriteProjectionCheckpointAsync(o.StreamID, o.GlobalSequence);
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
