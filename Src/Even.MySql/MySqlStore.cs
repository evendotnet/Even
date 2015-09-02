using DBHelpers;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Even.MySql
{
    public class MySqlStore : IStreamStore
    {
        public MySqlStore(string connectionString)
        {
            DB = new DBHelper(MySqlClientFactory.Instance, connectionString);
        }

        private DBHelper DB;

        #region Stream Writer

        public async Task<IWriteResult> WriteEventsAsync(IReadOnlyCollection<IRawStreamEvent> events)
        {
            var uniqueStreams = events
                .Select(e => e.StreamID)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var streamsQuery = $"SELECT StreamID, MAX(StreamSequence) FROM `events` WHERE StreamID IN ({InClause(uniqueStreams)}";

            using (var cn = DB.CreateConnection())
            {
                cn.Open();
                await DB.ExecuteNonQueryAsync("start transaction", cn);

                var checkpoint = await DB.ExecuteScalarAsync<long>("SELECT MAX(Checkpoint) FROM `events`", cn);
                var sequenceCounters = await DB.ExecuteDictionaryAsync<string, int>(streamsQuery);
                var sequencer = new Sequencer(checkpoint + 1, sequenceCounters);
                var insertSql = CreateInsertSql(events, sequencer);
                var result = sequencer.GetResult();

                long lastCheckpoint;
                try
                {
                    lastCheckpoint = await DB.ExecuteScalarAsync<long>(insertSql, cn);
                    DB.ExecuteNonQuery("commit", cn);
                    return result;
                }
                catch (MySqlException ex)
                {
                    DB.ExecuteNonQuery("rollback", cn);
                    throw;
                }
            }
        }

        public async Task<IWriteResult> WriteEventsStrictAsync(string streamId, int expectedSequence, IReadOnlyCollection<IRawEvent> events)
        {
            var streamsQuery = $"SELECT MAX(StreamSequence) FROM `events` WHERE StreamID = '{Escape(streamId)}';";

            using (var cn = DB.CreateConnection())
            {
                cn.Open();
                await DB.ExecuteNonQueryAsync("start transaction", cn);

                var currentSequence = await DB.ExecuteScalarAsync<int>(streamsQuery);

                if (currentSequence != expectedSequence) {
                    DB.ExecuteNonQuery("rollback", cn);
                    throw new UnexpectedSequenceException();
                }

                var checkpoint = await DB.ExecuteScalarAsync<long>("SELECT MAX(Checkpoint) FROM `events`", cn);
                var sequencer = new Sequencer(checkpoint + 1, new Dictionary<string, int> { { streamId, currentSequence + 1 } });

                var insertSql = CreateInsertSql(events.Select(e => EventFactory.CreateRawStreamEvent(e, streamId)), sequencer);
                var result = sequencer.GetResult();

                long lastCheckpoint;
                try
                {
                    lastCheckpoint = await DB.ExecuteScalarAsync<long>(insertSql, cn);
                    DB.ExecuteNonQuery("commit", cn);
                    return result;
                }
                catch (MySqlException ex)
                {
                    DB.ExecuteNonQuery("rollback", cn);
                    throw;
                }
            }
        }

        #endregion

        #region Stream Reader

        static readonly string BaseSelect = "select Checkpoint, EventID, StreamID, StreamSequence, EventName, UtcTimeStamp, Headers, Payload from `events`";

        public async Task ReadAsync(long initialCheckpoint, int maxEvents, Action<IRawPersistedEvent> readCallback, CancellationToken ct)
        {
            var limit = maxEvents < Int32.MaxValue ? " LIMIT " + maxEvents : String.Empty;
            var query = $"{BaseSelect} WHERE Checkpoint >= {initialCheckpoint} ORDER BY Checkpoint{limit};";

            using (var reader = await DB.ExecuteReaderAsync(query))
            {
                while (await reader.ReadAsync())
                {
                    var e = ReadPersistedEvent(reader);
                    readCallback(e);

                    if (ct.IsCancellationRequested)
                        return;
                }
            }
        }

        public async Task ReadStreamAsync(string streamId, int initialSequence, int maxEvents, Action<IRawPersistedEvent> readCallback, CancellationToken ct)
        {
            var limit = maxEvents < Int32.MaxValue ? " LIMIT " + maxEvents : String.Empty;
            var query = $"{BaseSelect} WHERE StreamID = '{Escape(streamId)}' AND StreamSequence >= {initialSequence} ORDER BY StreamSequence{limit};";

            using (var reader = await DB.ExecuteReaderAsync(query))
            {
                while (await reader.ReadAsync())
                {
                    var e = ReadPersistedEvent(reader);
                    readCallback(e);

                    if (ct.IsCancellationRequested)
                        return;
                }
            }
        }

        public Task<long> ReadHighestCheckpointAsync()
        {
            return DB.ExecuteScalarAsync<long>("SELECT MAX(Checkpoint) from `events`");
        }

        public Task<int> ReadHighestStreamSequenceAsync(string streamId)
        {
            return DB.ExecuteScalarAsync<int>($"SELECT MAX(StreamSequence) from `events` where StreamID = '{Escape(streamId)}'");
        }

        #endregion

        #region Helpers

        class Sequencer
        {
            public Sequencer(long initialCheckpoint, Dictionary<string, int> streamSequences)
            {
                _checkpoint = initialCheckpoint;
                _sequences = new Dictionary<string, int>(streamSequences, StringComparer.OrdinalIgnoreCase);
            }

            long _checkpoint;
            Dictionary<string, int> _sequences;
            List<IWrittenEventSequence> _written = new List<IWrittenEventSequence>();

            public int NextSequenceFor(string key, Guid eventId)
            {
                int sequence;

                if (_sequences.ContainsKey(key))
                    sequence = _sequences[key]++;
                else
                    sequence = _sequences[key] = 1;

                _written.Add(EventFactory.CreateWrittenEventSequence(eventId, _checkpoint++, sequence));

                return sequence;
            }

            public IWriteResult GetResult()
            {
                return new WriteResult
                {
                    Sequences = _written.ToList()
                };
            }
        }

        private static IRawPersistedEvent ReadPersistedEvent(DbDataReader reader)
        {
            return EventFactory.CreateRawPersistedEvent(
                reader.GetInt64(0),
                reader.GetGuid(1),
                reader.GetString(2),
                reader.GetInt32(3),
                reader.GetString(4),
                reader.GetDateTime(5),
                (byte[]) reader[6],
                (byte[]) reader[7]
            );
        }

        private static string InClause(IEnumerable<string> items)
        {
            return "'" + String.Join("', '", items.Select(i => Escape(i))) + "'";
        }

        private static string Escape(string s)
        {
            return MySqlHelper.EscapeString(s);
        }

        private static string BinaryString(byte[] bytes)
        {
            var sb = new StringBuilder(bytes.Length * 2);

            foreach (var b in bytes)
                sb.Append(b.ToString("x2"));

            return sb.ToString();
        }

        private static string DateString(DateTime dt)
        {
            return dt.ToString("yyyy-MM-dd HH:mm:ss.fffffff");
        }

        private static string CreateInsertSql(IEnumerable<IRawStreamEvent> events, Sequencer sequencer)
        {
            var sb = new StringBuilder();

            sb.Append("INSERT INTO events (EventID, StreamID, StreamSequence, EventName, UtcTimeStamp, Headers, Payload) VALUES ");

            var valueFormat = "(UNHEX('{0}'), '{1}', {2}, '{3}', '{4}', UNHEX('{5}'), UNHEX('{6}')), ";

            foreach (var e in events)
            {
                sb.AppendFormat(valueFormat,
                    BinaryString(e.EventID.ToByteArray()),
                    Escape(e.StreamID),
                    sequencer.NextSequenceFor(e.StreamID, e.EventID),
                    Escape(e.EventName),
                    DateString(e.UtcTimeStamp),
                    BinaryString(e.Headers),
                    BinaryString(e.Payload)
                );
            }

            sb.Length -= 2;

            sb.Append(";");

            return sb.ToString();
        }

        #endregion
    }
}
