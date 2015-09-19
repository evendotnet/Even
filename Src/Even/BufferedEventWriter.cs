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
    public class BufferedEventWriter : ReceiveActor
    {
        ISerializer _serializer;
        IEventStoreWriter _writer;
        IActorRef _dispatcher;

        LinkedList<BufferEntry> _buffer = new LinkedList<BufferEntry>();
        bool _flushRequested;

        public BufferedEventWriter(IEventStoreWriter writer, ISerializer serializer, IActorRef dispatcher)
        {
            Argument.Requires(writer != null, nameof(writer));
            Argument.Requires(serializer != null, nameof(serializer));
            Argument.Requires(dispatcher != null, nameof(dispatcher));

            _writer = writer;
            _serializer = serializer;
            _dispatcher = dispatcher;

            Receive<PersistenceRequest>(r => Enqueue(r));
            Receive<FlushBufferCommand>(_ => FlushBuffer());
        }

        void Enqueue(PersistenceRequest request)
        {
            _buffer.AddLast(new BufferEntry { Request = request, Sender = Sender });

            if (!_flushRequested)
            {
                Self.Tell(new FlushBufferCommand(), Self);
                _flushRequested = true;
            }
        }

        async Task FlushBuffer()
        {
            _flushRequested = false;

            if (_buffer.Count > 0)
            {
                var list = _buffer.ToList();
                _buffer.Clear();

                await FlushInternal(list);
            }
        }

        async Task FlushInternal(IReadOnlyList<BufferEntry> bufferEntries)
        {
            // these are used for logging in case the batch write fails
            ILoggingAdapter log = null;
            Guid operationId = Guid.NewGuid();

            // try writing all requests at once first. this should work everytime unless
            // some event exceeds some limit in the store or driver, or there is a bug in the code
            bool batchWritten;

            try
            {
                await Write(bufferEntries);
                batchWritten = true;
            }
            catch (DuplicatedEntryException) when (bufferEntries.Count == 1)
            {
                foreach (var e in bufferEntries)
                    e.Sender.Tell(new DuplicatedEntry(e.Request.PersistenceID));

                return;
            }
            catch (Exception ex) when (bufferEntries.Count == 1)
            {
                foreach (var e in bufferEntries)
                    e.Sender.Tell(new DuplicatedEntry(e.Request.PersistenceID));

                return;
            }
            catch (Exception ex)
            {
                batchWritten = false;
                log = Context.GetLogger();
                log.Warning("[{0}] Batch Write Failed: '{1}' - Retrying one at a time", operationId, ex.Message);
            }

            // if the batch was written successfully
            if (batchWritten)
            {
                // notify the senders
                foreach (var e in bufferEntries)
                    e.Sender.Tell(new PersistenceSuccess(e.Request.PersistenceID));

                return;
            }

            // if the batch wasn't written, fallback to writing one at a time
            foreach (var e in bufferEntries)
            {
                try
                {
                    await Write(new[] { e });
                    e.Sender.Tell(new PersistenceSuccess(e.Request.PersistenceID));
                    continue;
                }
                catch (DuplicatedEntryException ex)
                {
                    log.Warning("[{0}] Duplicated Entry for request '{1}': '{2}'", operationId, e.Request.PersistenceID, ex.Message);
                    e.Sender.Tell(new DuplicatedEntry(e.Request.PersistenceID));
                }
                catch (Exception ex)
                {
                    log.Warning("[{0}] Exception for request '{1}': '{2}'", operationId, e.Request.PersistenceID, ex.Message);
                    e.Sender.Tell(new PersistenceFailure(e.Request.PersistenceID, ex));
                }
            }

            log.Warning("[{0}] Write Finished", operationId);
        }

        async Task Write(IReadOnlyList<BufferEntry> bufferEntries)
        {
            // creates a list of "Event + Sender" entries
            var entries = bufferEntries.SelectMany(entry => entry.Request.Events.Select(@event => new { Event = @event, Sender = entry.Sender })).ToList();

            // serialize the events into raw events
            var rawEvents = UnpersistedRawEvent.FromUnpersistedEvents(entries.Select(e => e.Event), _serializer);

            // writes to the store
            await _writer.WriteAsync(rawEvents);

            // sends the events to the sender and to the dispatcher
            for (int i = 0, len = entries.Count; i < len; i++)
            {
                var sender = entries[i].Sender;
                var e = entries[i].Event;
                var re = rawEvents[i];

                var persistedEvent = PersistedEventFactory.Create(re.GlobalSequence, e);

                sender.Tell(persistedEvent);
                _dispatcher.Tell(persistedEvent);
            }
        }

        class FlushBufferCommand { }

        class BufferEntry
        {
            public PersistenceRequest Request;
            public IActorRef Sender;
        }
    }
}
