using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Even.Persistence
{
    public class InMemoryStore : IEventStore
    {
        List<PersistedRawEvent> _events = new List<PersistedRawEvent>();
        Dictionary<string, List<PersistedRawEvent>> _projections = new Dictionary<string, List<PersistedRawEvent>>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, long> _projectionCheckpoints = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

        public List<PersistedRawEvent> GetEvents()
        {
            lock (_events)
            {
                return _events.ToList();
            }
        }

        #region StreamStore

        public Task WriteEventsAsync(IReadOnlyCollection<UnpersistedRawEvent> events)
        {
            lock (_events)
            {
                var globalSequence = _events.Count + 1;

                foreach (var e in events)
                {
                    e.SetGlobalSequence(globalSequence++);

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

        public Task WriteEventsAsync(string streamId, int expectedSequence, IReadOnlyCollection<UnpersistedRawEvent> events)
        {
            lock (_events)
            {
                var streamCount = _events
                    .Where(e => String.Equals(e.StreamID, streamId, StringComparison.OrdinalIgnoreCase))
                    .Count();

                if (expectedSequence >= 0)
                {
                    if (expectedSequence != streamCount)
                        throw new UnexpectedStreamSequenceException();
                }

                var globalSequence = _events.Count + 1;

                foreach (var e in events)
                {
                    e.SetGlobalSequence(globalSequence++);

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

        public Task<long> ReadHighestGlobalSequence()
        {
            lock (_events)
            {
                return Task.FromResult((long) _events.Count);
            }
        }

        public Task<int> ReadHighestStreamSequenceAsync(string streamId)
        {
            lock (_events)
            {
                var count = _events
                    .Where(e => String.Equals(e.StreamID, streamId, StringComparison.OrdinalIgnoreCase))
                    .Count();

                return Task.FromResult(count);
            }
        }

        public Task ReadAsync(long initialCheckpoint, int maxEvents, Action<IPersistedRawEvent> readCallback, CancellationToken ct)
        {
            lock (_events)
            {
                foreach (var e in _events.Skip((int)initialCheckpoint).Take(maxEvents))
                    readCallback(e);
            }

            return Task.CompletedTask;
        }

        public Task ReadStreamAsync(string streamId, int initialSequence, int maxEvents, Action<IPersistedRawEvent> readCallback, CancellationToken ct)
        {
            lock (_events)
            {
                var streamEvents = _events
                    .Where(e => String.Equals(e.StreamID, streamId, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(e => e.GlobalSequence);

                foreach (var e in streamEvents.Skip(initialSequence).Take(maxEvents))
                    readCallback(e);
            }

            return Task.CompletedTask;
        }

        public Task WriteProjectionIndexAsync(string streamId, IReadOnlyCollection<long> globalSequences)
        {
            return Task.CompletedTask;
        }

        public Task WriteProjectionCheckpointAsync(string streamId, long globalSequence)
        {
            return Task.CompletedTask;
        }

        public Task<long> ReadProjectionCheckpointAsync(string streamId)
        {
            return Task.FromResult(0L);
        }

        public Task<long> ReadHighestProjectionGlobalSequenceAsync(string streamId)
        {
            return Task.FromResult(0L);
        }

        public Task<int> ReadHighestProjectionStreamSequenceAsync(string streamId)
        {
            return Task.FromResult(0);
        }

        public Task ReadIndexedProjectionStreamAsync(string streamId, int initialSequence, int maxEvents, Action<IPersistedRawEvent> readCallback, CancellationToken ct)
        {
            return Task.CompletedTask;
        }

        #endregion
    }
}
