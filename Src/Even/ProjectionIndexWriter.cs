using Akka.Actor;
using Akka.Event;
using Even.Messages;
using Even.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even
{
    public class ProjectionIndexWriter : ReceiveActor
    {
        IProjectionStoreWriter _writer;
        GlobalOptions _options;

        LinkedList<BufferEntry> _buffer = new LinkedList<BufferEntry>();
        bool _flushRequested;

        public static Props CreateProps(IProjectionStoreWriter writer, GlobalOptions options)
        {
            Argument.RequiresNotNull(writer, nameof(writer));
            Argument.RequiresNotNull(options, nameof(options));

            return Props.Create<ProjectionIndexWriter>(writer, options);
        }

        public ProjectionIndexWriter(IProjectionStoreWriter writer, GlobalOptions options)
        {
            _writer = writer;
            _options = options;

            Receive<ProjectionIndexPersistenceRequest>(request => Enqueue(request));
            Receive<FlushBufferCommand>(_ => FlushBuffer());
        }

        void Enqueue(ProjectionIndexPersistenceRequest request)
        {
            _buffer.AddLast(new BufferEntry { Request = request, Sender = Sender });

            if (!_flushRequested)
            {
                _flushRequested = true;
                Context.System.Scheduler.ScheduleTellOnce(_options.IndexWriterFlushDelay, Self, new FlushBufferCommand(), Self);
            }
        }

        async Task FlushBuffer()
        {
            _flushRequested = false;

            if (_buffer.Count == 0)
                return;

            // groups all requests by sender and stream and issues a write for each one at a time
            var re = from e in _buffer
                     group e by new { e.Sender, e.Request.ProjectionStreamID } into g
                     select new WriteEntry
                     {
                         Sender = g.Key.Sender,
                         StreamID = g.Key.ProjectionStreamID,
                         Requests = g.Select(o => o.Request).ToList()
                     };

            foreach (var e in re)
            {
                try
                {
                    await Write(e);
                }
                catch (Exception ex) when (ex is MissingIndexEntryException || ex is UnexpectedStreamSequenceException || ex is DuplicatedEntryException)
                {
                    e.Sender.Tell(new ProjectionIndexInconsistencyDetected());
                }
                catch (Exception ex)
                {
                    Context.GetLogger().Error(ex, "Error writing projection index");
                }
            }

            _buffer.Clear();
        }

        async Task Write(WriteEntry entry)
        {
            var requests = entry.Requests.OrderBy(o => o.ProjectionStreamSequence).ToList();

            if (!requests.Select(e => e.ProjectionStreamSequence).IsSequential())
                throw new MissingIndexEntryException();

            var firstSequence = requests.First().ProjectionStreamSequence;
            var globalSequences = requests.Select(e => e.GlobalSequence).ToList();

            await _writer.WriteProjectionIndexAsync(entry.StreamID, firstSequence - 1, globalSequences);
        }

        class FlushBufferCommand { }

        class BufferEntry
        {
            public IActorRef Sender;
            public ProjectionIndexPersistenceRequest Request;
        }

        class WriteEntry
        {
            public IActorRef Sender;
            public string StreamID;
            public IReadOnlyList<ProjectionIndexPersistenceRequest> Requests;
        }
    }
}
