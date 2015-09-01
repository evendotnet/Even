using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Even.Persistence
{
    public class InMemoryStore : IStreamStore
    {
        public IReadOnlyCollection<IRawPersistedEvent> GetEvents()
        {
            lock (_events)
            {
                return _events.ToList();
            }
        }

        List<IRawPersistedEvent> _events = new List<IRawPersistedEvent>();

        #region StoreWriter

        public Task<IWriteResult> WriteEventsAsync(IEnumerable<IRawStreamEvent> events)
        {
            lock (_events)
            {
                var sequenceData = events.Select(e => e.StreamID)
                    .Distinct()
                    .ToDictionary(s => s, s => GetLastStreamSequence(s));

                var sequencer = new Sequencer(sequenceData);
                var checkpoint = _events.Count + 1;

                var eventsToStore = events.Select(e => new InternalEvent
                {
                    Checkpoint = checkpoint++,
                    StreamSequence = sequencer.GetNext(e.StreamID),
                    StreamID = e.StreamID.ToLowerInvariant(),
                    IRawEvent = e
                }).ToList();

                _events.AddRange(eventsToStore);

                var result = new WriteResult
                {
                    Sequences = eventsToStore.Select(e => EventFactory.CreateWrittenEventSequence(e.EventID, e.Checkpoint, e.StreamSequence)).ToList()
                };

                return Task.FromResult<IWriteResult>(result);
            }
        }

        public Task<IWriteResult> WriteEventsStrictAsync(string streamId, int expectedSequence, IEnumerable<IRawEvent> events)
        {
            lock (_events)
            {
                var sequence = GetLastStreamSequence(streamId);

                if (sequence != expectedSequence)
                    throw new StrictEventWriteException();

                var checkpoint = _events.Count + 1;

                var eventsToStore = events.Select(e => new InternalEvent
                {
                    Checkpoint = checkpoint++,
                    StreamSequence = ++sequence,
                    StreamID = streamId.ToLowerInvariant(),
                    IRawEvent = e
                }).ToList();

                _events.AddRange(eventsToStore);

                var result = new WriteResult
                {
                    Sequences = eventsToStore.Select(e => EventFactory.CreateWrittenEventSequence(e.EventID, e.Checkpoint, e.StreamSequence)).ToList()
                };

                return Task.FromResult<IWriteResult>(result);
            }
        }

        #endregion

        #region StoreReader

        public Task<long> ReadHighestCheckpointAsync()
        {
            lock (_events)
            {
                return Task.FromResult<long>(_events.Count);
            }
        }

        public Task<int> ReadHighestStreamSequenceAsync(string streamId)
        {
            lock (_events)
            {
                return Task.FromResult(GetLastStreamSequence(streamId));
            }
        }

        public Task ReadAsync(long initialCheckpoint, int maxEvents, Action<IRawPersistedEvent> readCallback, CancellationToken ct)
        {
            lock (_events)
            {
                var re = from e in _events
                         where e.Checkpoint >= initialCheckpoint
                         orderby e.Checkpoint
                         select e;

                foreach (var e in re.Take(maxEvents))
                    readCallback(e);

                return Task.CompletedTask;
            }
        }

        public Task ReadStreamAsync(string streamId, int initialSequence, int maxEvents, Action<IRawPersistedEvent> readCallback, CancellationToken ct)
        {
            streamId = streamId.ToLowerInvariant();

            lock (_events)
            {
                var re = from e in _events
                         where e.StreamID == streamId && e.StreamSequence >= initialSequence
                         orderby e.StreamSequence
                         select e;

                foreach (var e in re.Take(maxEvents))
                    readCallback(e);

                return Task.CompletedTask;
            }
        }

        #endregion

        #region Helpers

        class Sequencer
        {
            public Sequencer(Dictionary<string, int> data = null)
            {
                _counters = data ?? new Dictionary<string, int>();
            }

            Dictionary<string, int> _counters = new Dictionary<string, int>();

            public int GetNext(string streamId)
            {
                if (!_counters.ContainsKey(streamId))
                    _counters[streamId] = 0;

                return _counters[streamId]++;
            }
        }

        class InternalEvent : IRawPersistedEvent
        {
            public long Checkpoint { get; set; }
            public int StreamSequence { get; set; }
            public string StreamID { get; set; }

            public IRawEvent IRawEvent { get; set; }

            public Guid EventID => IRawEvent.EventID;
            public string EventName => IRawEvent.EventName;
            public byte[] Headers => IRawEvent.Headers;
            public byte[] Payload => IRawEvent.Payload;
            public DateTime UtcTimeStamp => IRawEvent.UtcTimeStamp;
        }

        private int GetLastStreamSequence(string streamId)
        {
            streamId = streamId.ToLowerInvariant();

            var re = from e in _events
                     where e.StreamID == streamId
                     select e.StreamSequence;

            return re.Any() ? re.Max() : 0;
        }

        #endregion
    }
}
