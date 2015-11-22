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
                        Stream = e.Stream,
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

        public Task WriteStreamAsync(Stream stream, int expectedSequence, IReadOnlyCollection<IUnpersistedRawEvent> events)
        {
            lock (_events)
            {
                var streamCount = _events
                    .Where(e => e.Stream.Equals(stream))
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
                        Stream = stream,
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

        public Task ReadStreamAsync(Stream stream, int initialSequence, int count, Action<IPersistedRawEvent> readCallback, CancellationToken ct)
        {
            Argument.Requires<ArgumentOutOfRangeException>(initialSequence >= 0, nameof(initialSequence));
            Argument.Requires<ArgumentOutOfRangeException>(count >= 0 || count == EventCount.Unlimited, nameof(count));

            lock (_events)
            {
                var events = _events
                    .Where(e => stream.Equals(e.Stream))
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

        public Task ClearProjectionIndexAsync(Stream stream)
        {
            var key = stream.ToHexString();

            lock (_projectionCheckpoints)
            {
                if (_projectionCheckpoints.ContainsKey(key))
                    _projectionCheckpoints.Remove(key);
            }

            lock (_projectionIndexes)
            {
                if (_projectionIndexes.ContainsKey(key))
                    _projectionIndexes.Remove(key);
            }

            return Task.CompletedTask;
        }

        public Task WriteProjectionIndexAsync(Stream stream, int expectedSequence, IReadOnlyCollection<long> globalSequences)
        {
            lock (_projectionIndexes)
            {
                List<long> projection;

                var key = stream.ToHexString();

                if (!_projectionIndexes.TryGetValue(key, out projection))
                    _projectionIndexes.Add(key, projection = new List<long>());

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

        public Task WriteProjectionCheckpointAsync(Stream stream, long globalSequence)
        {
            lock (_projectionCheckpoints)
            {
                _projectionCheckpoints[stream.ToHexString()] = globalSequence;
            }

            return Task.CompletedTask;
        }

        public Task<long> ReadProjectionCheckpointAsync(Stream stream)
        {
            lock (_projectionCheckpoints)
            {
                long checkpoint;

                if (_projectionCheckpoints.TryGetValue(stream.ToHexString(), out checkpoint))
                    return Task.FromResult(checkpoint);
            }

            return Task.FromResult(0L);
        }

        public Task<long> ReadHighestIndexedProjectionGlobalSequenceAsync(Stream stream)
        {
            lock (_projectionIndexes)
            {
                List<long> projection;

                if (_projectionIndexes.TryGetValue(stream.ToHexString(), out projection))
                    return Task.FromResult(projection.LastOrDefault());
            }

            return Task.FromResult(0L);
        }

        public Task<int> ReadHighestIndexedProjectionStreamSequenceAsync(Stream stream)
        {
            lock (_projectionIndexes)
            {
                List<long> projection;

                if (_projectionIndexes.TryGetValue(stream.ToHexString(), out projection))
                    return Task.FromResult(projection.Count);
            }

            return Task.FromResult(0);
        }

        public Task ReadIndexedProjectionStreamAsync(Stream stream, int initialSequence, int count, Action<IPersistedRawEvent> readCallback, CancellationToken ct)
        {
            lock (_projectionIndexes)
            {
                List<long> projection;

                if (_projectionIndexes.TryGetValue(stream.ToHexString(), out projection))
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
