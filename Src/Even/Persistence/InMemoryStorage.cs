using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Even.Persistence
{
    public class InMemoryStorage : IStorageDriver
    {
        InMemoryStore _store = new InMemoryStore();

        public IStorageReader CreateReader()
        {
            return new InMemoryStorageReader(_store);
        }

        public IStorageWriter CreateWriter()
        {
            return new InMemoryStorageWriter(_store);
        }

        public List<IRawStorageEvent> GetEvents()
        {
            lock (_store.Events)
            {
                return _store.Events.Cast<IRawStorageEvent>().ToList();
            }
        }

        internal class InMemoryStore
        {
            public List<MemoryEvent> Events = new List<MemoryEvent>();
        }

        internal class MemoryEvent : IRawStorageEvent
        {
            public long Checkpoint { get; set; }
            public Guid EventID { get; set; }
            public string EventName { get; set; }
            public byte[] Headers { get; set; }
            public byte[] Payload { get; set; }
            public string StreamID { get; set; }
            public int StreamSequence { get; set; }
        }

        internal class InMemoryStorageWriter : IStorageWriter
        {
            public InMemoryStorageWriter(InMemoryStore store)
            {
                _store = store;
            }

            InMemoryStore _store;

            public Task WriteEvents(IEnumerable<IRawStorageEvent> events, Action<IRawStorageEvent, long> writtenCallback)
            {
                lock (_store.Events)
                {
                    var list = _store.Events;

                    foreach (var e in events)
                    {
                        var checkpoint = list.Count + 1;

                        var me = new MemoryEvent
                        {
                            Checkpoint = checkpoint,
                            EventID = e.EventID,
                            EventName = e.EventName,
                            Headers = e.Headers,
                            Payload = e.Payload,
                            StreamID = e.StreamID,
                            StreamSequence = e.StreamSequence
                        };
                        
                        list.Add(me);
                        writtenCallback(e, checkpoint);
                    }
                }

                return Task.CompletedTask;
            }
        }

        internal class InMemoryStorageReader : IStorageReader
        {
            public InMemoryStorageReader(InMemoryStore store)
            {
                _store = store;
            }

            private InMemoryStore _store;

            public Task<long> GetHighestCheckpointAsync()
            {
                lock (_store.Events)
                {
                    return Task.FromResult((long)_store.Events.Count);
                }
            }

            public Task<int> GetHighestStreamSequenceAsync(string streamId)
            {
                lock (_store.Events)
                {
                    var re = from e in _store.Events
                             where e.StreamID.Equals(streamId, StringComparison.OrdinalIgnoreCase)
                             select e.StreamSequence;

                    return Task.FromResult(re.Max());
                }
            }

            public Task<IRawAggregateSnapshot> ReadAggregateSnapshotAsync(string streamId, CancellationToken ct)
            {
                return Task.FromResult<IRawAggregateSnapshot>(null);
            }

            public Task ReadAggregateStreamAsync(string streamId, int initialSequence, int maxEvents, Action<IRawStorageEvent> readCallback, CancellationToken ct)
            {
                lock (_store.Events)
                {
                    var re = from e in _store.Events
                             where e.StreamID.Equals(streamId, StringComparison.OrdinalIgnoreCase) && e.StreamSequence >= initialSequence
                             orderby e.StreamSequence
                             select e;

                    foreach (var rawEvent in re)
                    {
                        readCallback(rawEvent);
                    }

                    return Task.CompletedTask;
                }
            }

            public Task QueryCheckpointsAsync(EventStoreQuery esQuery, long initialCheckpoint, int maxEvents, Func<long, bool> readCallback, CancellationToken ct)
            {
                throw new NotImplementedException();
            }

            public Task QueryEventsAsync(EventStoreQuery esQuery, long initialCheckpoint, int maxEvents, Func<IRawStorageEvent, bool> readCallback, CancellationToken ct)
            {
                throw new NotImplementedException();
            }
        }

    }
}
