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
        public InMemoryStore _store = new InMemoryStore();

        public IStorageReader CreateReader()
        {
            return _store;
        }

        public IStorageWriter CreateWriter()
        {
            return _store;
        }

        public class InMemoryStore: IStorageWriter, IStorageReader
        {
            public List<MemoryEvent> _events = new List<MemoryEvent>();
            public Dictionary<string, List<IProjectionStreamIndex>> _projections = new Dictionary<string, List<IProjectionStreamIndex>>(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, long> _projectionCheckpoints = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, IRawAggregateSnapshot> _snapshots = new Dictionary<string, IRawAggregateSnapshot>(StringComparer.OrdinalIgnoreCase);

            private List<IProjectionStreamIndex> GetOrCreateIndex(string streamId)
            {
                List<IProjectionStreamIndex> index;

                if (!_projections.TryGetValue(streamId, out index))
                {
                    index = new List<IProjectionStreamIndex>();
                    _projections[streamId] = index;
                }

                return index;
            }

            #region Storage writer

            public Task WriteEvents(IEnumerable<IRawStorageEvent> events, Action<IRawStorageEvent, long> writtenCallback)
            {
                lock (_events)
                {
                    foreach (var e in events)
                    {
                        var checkpoint = _events.Count + 1;

                        _events.Add(new MemoryEvent
                        {
                            Checkpoint = checkpoint,
                            EventID = e.EventID,
                            EventName = e.EventName,
                            Headers = e.Headers,
                            Payload = e.Payload,
                            StreamID = e.StreamID,
                            StreamSequence = e.StreamSequence
                        });

                        writtenCallback(e, checkpoint);
                    }

                    return Task.CompletedTask;
                }
            }

            public Task ClearProjectionIndex(string projectionId)
            {
                lock (_projections)
                {
                    _projections[projectionId] = null;
                }

                return Task.CompletedTask;
            }

            public Task WriteProjectionStreamIndex(IEnumerable<IProjectionStreamIndex> entries)
            {
                lock (_projections) {
                    foreach (var e in entries)
                    {
                        var index = GetOrCreateIndex(e.ProjectionID);
                        index.Add(e);
                    }
                }

                return Task.CompletedTask;
            }

            public Task WriteSnapshot(string streamId, IRawAggregateSnapshot snapshot)
            {
                lock (_snapshots)
                {
                    _snapshots[streamId] = snapshot;
                }

                return Task.CompletedTask;
            }

            #endregion

            #region Reader

            public Task<long> GetHighestCheckpointAsync()
            {
                lock (_events)
                {
                    return Task.FromResult((long)_events.Count);
                }
            }

            public Task<int> GetHighestStreamSequenceAsync(string streamId)
            {
                lock (_events)
                {
                    var re = from e in _events
                             where String.Equals(e.StreamID, streamId, StringComparison.OrdinalIgnoreCase)
                             select e.StreamSequence;

                    return Task.FromResult(re.Max());
                }
            }

            public Task<IRawAggregateSnapshot> ReadStreamSnapshotAsync(string streamId, CancellationToken ct)
            {
                lock (_snapshots)
                {
                    IRawAggregateSnapshot snapshot = null;
                    _snapshots.TryGetValue(streamId, out snapshot);
                    return Task.FromResult(snapshot);
                }
            }

            public Task ReadStreamAsync(string projectionStreamId, int initialSequence, int maxEvents, Action<IRawStorageEvent> readCallback, CancellationToken ct)
            {
                List<MemoryEvent> events;

                lock (_events)
                {
                    var re = from e in _events
                             where String.Equals(e.StreamID, projectionStreamId, StringComparison.OrdinalIgnoreCase)
                             orderby e.StreamSequence
                             select e;

                    events = re.Take(maxEvents).ToList();
                }

                foreach (var e in events)
                    readCallback(e);

                return Task.CompletedTask;
            }

            public Task ReadAsync(long initialCheckpoint, int maxEvents, Action<IRawStorageEvent> readCallback, CancellationToken ct)
            {
                List<MemoryEvent> all;

                lock (_events)
                {
                    all = _events
                            .Where(e => e.Checkpoint >= initialCheckpoint)
                            .Take(maxEvents)
                            .ToList();
                }

                foreach (var e in all)
                    readCallback(e);

                return Task.CompletedTask;
            }

            public Task<long> GetHighestProjectionCheckpoint(string projectionStreamId)
            {
                long checkpoint;

                lock (_projectionCheckpoints) {

                    if (_projectionCheckpoints.TryGetValue(projectionStreamId, out checkpoint))
                        return Task.FromResult(checkpoint);
                }

                lock (_projections)
                {
                    var p = GetOrCreateIndex(projectionStreamId);
                    return Task.FromResult(p.Max(e => e.Checkpoint));
                }
            }

            public Task<int> GetHighestProjectionStreamSequenceAsync(string projectionStreamId)
            {
                lock (_projections)
                {
                    var p = GetOrCreateIndex(projectionStreamId);
                    return Task.FromResult(p.Max(e => e.ProjectionSequence));
                }
            }

            public Task ReadProjectionEventStreamAsync(string projectionStreamId, int initialSequence, int maxEvents, Action<IRawStorageProjectionEvent> readCallback, CancellationToken ct)
            {
                List<IProjectionStreamIndex> projection;

                lock (_projections)
                {
                    projection = GetOrCreateIndex(projectionStreamId).ToList();
                }

                List<RawStorageProjectionEvent> events;

                lock (_events)
                {
                    var re = from e in _events
                             join p in projection on e.Checkpoint equals p.Checkpoint
                             select new RawStorageProjectionEvent(p.ProjectionID, p.ProjectionSequence, e);

                    events = re.Take(maxEvents).ToList();
                }

                foreach (var e in events)
                    readCallback(e);

                return Task.CompletedTask;
            }

            #endregion

            public class MemoryEvent : IRawStorageEvent
            {
                public long Checkpoint { get; set; }
                public Guid EventID { get; set; }
                public string EventName { get; set; }
                public byte[] Headers { get; set; }
                public byte[] Payload { get; set; }
                public string StreamID { get; set; }
                public int StreamSequence { get; set; }
            }
        }

        //internal class InMemoryStorageWriter : IStorageWriter
        //{
        //    public InMemoryStorageWriter(InMemoryStore store)
        //    {
        //        _store = store;
        //    }

        //    InMemoryStore _store;

        //    public Task WriteEvents(IEnumerable<IRawStorageEvent> events, Action<IRawStorageEvent, long> writtenCallback)
        //    {
        //        lock (_store.Events)
        //        {
        //            var list = _store.Events;

        //            foreach (var e in events)
        //            {
        //                var checkpoint = list.Count + 1;

        //                var me = new MemoryEvent
        //                {
        //                    Checkpoint = checkpoint,
        //                    EventID = e.EventID,
        //                    EventName = e.EventName,
        //                    Headers = e.Headers,
        //                    Payload = e.Payload,
        //                    StreamID = e.StreamID,
        //                    StreamSequence = e.StreamSequence
        //                };
                        
        //                list.Add(me);
        //                writtenCallback(e, checkpoint);
        //            }
        //        }

        //        return Task.CompletedTask;
        //    }

        //    public Task WriteSnapshot(string streamId, IRawAggregateSnapshot snapshot)
        //    {
        //        return Task.CompletedTask;
        //    }

        //    public Task WriteProjectionStreamIndex(IEnumerable<IProjectionStreamIndex> entries)
        //    {
        //        return Task.CompletedTask;
        //    }

        //    public Task ClearProjectionIndex(string projectionId)
        //    {
        //        return Task.CompletedTask;
        //    }
        //}

        //internal class InMemoryStorageReader : IStorageReader
        //{
        //    public InMemoryStorageReader(InMemoryStore store)
        //    {
        //        _store = store;
        //    }

        //    private InMemoryStore _store;

        //    public Task<long> GetHighestCheckpointAsync()
        //    {
        //        lock (_store.Events)
        //        {
        //            return Task.FromResult((long)_store.Events.Count);
        //        }
        //    }

        //    public Task<int> GetHighestStreamSequenceAsync(string streamId)
        //    {
        //        lock (_store.Events)
        //        {
        //            var re = from e in _store.Events
        //                     where e.StreamID.Equals(streamId, StringComparison.OrdinalIgnoreCase)
        //                     select e.StreamSequence;

        //            return Task.FromResult(re.Max());
        //        }
        //    }

        //    public async Task<Dictionary<string, int>> GetHighestStreamSequenceAsync(string[] streamIds)
        //    {
        //        var dict = new Dictionary<string, int>();

        //        foreach (var id in streamIds)
        //            dict.Add(id, await GetHighestStreamSequenceAsync(id));

        //        return dict;
        //    }

        //    public Task<IRawAggregateSnapshot> ReadStreamSnapshotAsync(string streamId, CancellationToken ct)
        //    {
        //        return Task.FromResult<IRawAggregateSnapshot>(null);
        //    }

        //    public Task ReadStreamAsync(string streamId, int initialSequence, int maxEvents, Action<IRawStorageEvent> readCallback, CancellationToken ct)
        //    {
        //        lock (_store.Events)
        //        {
        //            var re = from e in _store.Events
        //                     where e.StreamID.Equals(streamId, StringComparison.OrdinalIgnoreCase) && e.StreamSequence >= initialSequence
        //                     orderby e.StreamSequence
        //                     select e;

        //            foreach (var rawEvent in re)
        //            {
        //                readCallback(rawEvent);
        //            }

        //            return Task.CompletedTask;
        //        }
        //    }

        //    public Task ReadAsync(long initialCheckpoint, int maxEvents, Action<IRawStorageEvent> readCallback, CancellationToken ct)
        //    {
        //        lock (_store.Events)
        //        {
        //            var re = from e in _store.Events
        //                     where e.Checkpoint > initialCheckpoint
        //                     select e;

        //            foreach (var e in re.Take(maxEvents))
        //                readCallback(e);
        //        }

        //        return Task.CompletedTask;
        //    }

        //    public Task<long> GetHighestProjectionCheckpoint(string projectionId)
        //    {
        //        throw new NotImplementedException();
        //    }

        //    public Task<int> GetHighestProjectionStreamSequenceAsync(string projectionId)
        //    {
        //        throw new NotImplementedException();
        //    }

        //    public Task ReadProjectionEventStreamAsync(string projectionId, int initialSequence, int maxEvents, Action<IRawStorageProjectionEvent> readCallback, CancellationToken ct)
        //    {
        //        throw new NotImplementedException();
        //    }
        //}
    }
}
