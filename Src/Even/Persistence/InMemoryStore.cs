using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Even.Persistence
{
    public class InMemoryStore : IEventStore
    {
        List<PersistedRawEvent> _events = new List<PersistedRawEvent>();
        Dictionary<string, long> _projectionCheckpoints = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, List<long>> _projectionIndexes = new Dictionary<string, List<long>>();

        public List<PersistedRawEvent> GetEvents()
        {
            lock (_events)
            {
                return _events.ToList();
            }
        }

        #region Events

        public Task WriteAsync(IReadOnlyCollection<IUnpersistedRawStreamEvent> events)
        {
            lock (_events)
            {
                var globalSequence = _events.Count + 1;

                var existingEvents = _events.Select(e => e.EventID);

                if (existingEvents.Intersect(events.Select(e => e.EventID)).Any())
                    throw new DuplicatedEntryException();

                foreach (var e in events)
                {
                    e.GlobalSequence = globalSequence++;

                    var p = new PersistedRawEvent
                    {
                        GlobalSequence = e.GlobalSequence,
                        EventID = e.EventID,
                        StreamID = e.StreamID,
                        EventType = e.EventType,
                        UtcTimestamp = e.UtcTimestamp,
                        Metadata = e.Metadata,
                        Payload = e.Payload
                    };

                    _events.Add(p);
                }
            }

            return Task.CompletedTask;
        }

        public Task WriteStreamAsync(string streamId, int expectedSequence, IReadOnlyCollection<IUnpersistedRawEvent> events)
        {
            lock (_events)
            {
                var streamCount = _events
                    .Where(e => String.Equals(e.StreamID, streamId, StringComparison.OrdinalIgnoreCase))
                    .Count();

                if (expectedSequence != ExpectedSequence.Any)
                {
                    if (expectedSequence == ExpectedSequence.None && streamCount > 0)
                        throw new UnexpectedStreamSequenceException();

                    if (expectedSequence >= 0 && expectedSequence != streamCount)
                        throw new UnexpectedStreamSequenceException();
                }

                var globalSequence = _events.Count + 1;

                foreach (var e in events)
                {
                    e.GlobalSequence = globalSequence++;

                    var p = new PersistedRawEvent
                    {
                        GlobalSequence = e.GlobalSequence,
                        EventID = e.EventID,
                        StreamID = streamId,
                        EventType = e.EventType,
                        UtcTimestamp = e.UtcTimestamp,
                        Metadata = e.Metadata,
                        Payload = e.Payload
                    };

                    _events.Add(p);
                }
            }

            return Task.CompletedTask;
        }

        public Task<long> ReadHighestGlobalSequenceAsync()
        {
            lock (_events)
            {
                return Task.FromResult((long)_events.Count);
            }
        }

        public Task ReadAsync(long initialSequence, int count, Action<IPersistedRawEvent> readCallback, CancellationToken ct)
        {
            Argument.Requires<ArgumentOutOfRangeException>(initialSequence >= 0, nameof(initialSequence));
            Argument.Requires<ArgumentOutOfRangeException>(count >= 0 || count == EventCount.Unlimited, nameof(count));

            lock (_events)
            {
                IEnumerable<PersistedRawEvent> events = _events;

                if (initialSequence > 0)
                    events = events.Where(e => e.GlobalSequence >= initialSequence);

                if (count >= 0)
                    events = events.Take(count);

                foreach (var e in events)
                {
                    if (ct.IsCancellationRequested)
                        break;

                    readCallback(e);
                }
            }

            return Task.CompletedTask;
        }

        public Task ReadStreamAsync(string streamId, int initialSequence, int count, Action<IPersistedRawEvent> readCallback, CancellationToken ct)
        {
            Argument.Requires<ArgumentOutOfRangeException>(initialSequence >= 0, nameof(initialSequence));
            Argument.Requires<ArgumentOutOfRangeException>(count >= 0 || count == EventCount.Unlimited, nameof(count));

            lock (_events)
            {
                var events = _events
                    .Where(e => String.Equals(e.StreamID, streamId, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(e => e.GlobalSequence)
                    .AsEnumerable();

                if (initialSequence > 0)
                    events = events.Skip(initialSequence - 1);

                if (count >= 0)
                    events = events.Take(count);

                foreach (var e in events)
                {
                    if (ct.IsCancellationRequested)
                        break;

                    readCallback(e);
                }
            }

            return Task.CompletedTask;
        }

        #endregion

        #region Projections

        public Task ClearProjectionIndexAsync(string streamId)
        {
            lock (_projectionCheckpoints)
            {
                if (_projectionCheckpoints.ContainsKey(streamId))
                    _projectionCheckpoints.Remove(streamId);
            }

            lock (_projectionIndexes)
            {
                if (_projectionIndexes.ContainsKey(streamId))
                    _projectionIndexes.Remove(streamId);
            }

            return Task.CompletedTask;
        }

        public Task WriteProjectionIndexAsync(string streamId, int expectedSequence, IReadOnlyCollection<long> globalSequences)
        {
            lock (_projectionIndexes)
            {
                List<long> projection;

                if (!_projectionIndexes.TryGetValue(streamId, out projection))
                    _projectionIndexes.Add(streamId, projection = new List<long>());

                if (projection.Count != expectedSequence)
                    throw new UnexpectedStreamSequenceException();

                if (globalSequences.Intersect(projection).Any())
                    throw new DuplicatedEntryException();

                lock (_events)
                {
                    projection.AddRange(globalSequences);
                }
            }

            return Task.CompletedTask;
        }

        public Task WriteProjectionCheckpointAsync(string streamId, long globalSequence)
        {
            lock (_projectionCheckpoints)
            {
                _projectionCheckpoints[streamId] = globalSequence;
            }

            return Task.CompletedTask;
        }

        public Task<long> ReadProjectionCheckpointAsync(string streamId)
        {
            lock (_projectionCheckpoints)
            {
                long checkpoint;

                if (_projectionCheckpoints.TryGetValue(streamId, out checkpoint))
                    return Task.FromResult(checkpoint);
            }

            return Task.FromResult(0L);
        }

        public Task<long> ReadHighestIndexedProjectionGlobalSequenceAsync(string streamId)
        {
            lock (_projectionIndexes)
            {
                List<long> projection;

                if (_projectionIndexes.TryGetValue(streamId, out projection))
                    return Task.FromResult(projection.LastOrDefault());
            }

            return Task.FromResult(0L);
        }

        public Task<int> ReadHighestIndexedProjectionStreamSequenceAsync(string streamId)
        {
            lock (_projectionIndexes)
            {
                List<long> projection;

                if (_projectionIndexes.TryGetValue(streamId, out projection))
                    return Task.FromResult(projection.Count);
            }

            return Task.FromResult(0);
        }

        public Task ReadIndexedProjectionStreamAsync(string streamId, int initialSequence, int count, Action<IPersistedRawEvent> readCallback, CancellationToken ct)
        {
            lock (_projectionIndexes)
            {
                List<long> projection;

                if (_projectionIndexes.TryGetValue(streamId, out projection))
                {
                    IEnumerable<PersistedRawEvent> events;

                    lock (_events)
                    {
                        events = from s in projection
                                 join e in _events on s equals e.GlobalSequence
                                 orderby e.GlobalSequence
                                 select e;

                        if (initialSequence > 0)
                            events = events.Skip(initialSequence - 1);

                        if (count >= 0)
                            events = events.Take(count);

                        events = events.ToList();
                    }

                    foreach (var e in events)
                    {
                        if (ct.IsCancellationRequested)
                            break;

                        readCallback(e);
                    }
                }
            }

            return Task.CompletedTask;
        }

        #endregion
    }
}
