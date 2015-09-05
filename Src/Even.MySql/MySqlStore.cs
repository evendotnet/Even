using DBHelpers;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Even.MySql
{
    public class MySqlStore : IStreamStore, IProjectionStore
    {
        public MySqlStore(string connectionString)
        {
            DB = new DBHelper(MySqlClientFactory.Instance, connectionString);
        }

        private DBHelper DB;

        #region IStreamStore

        public async Task WriteEventsAsync(string streamId, int expectedSequence, IReadOnlyCollection<UnpersistedRawEvent> events)
        {
            var maxStreamSequenceQuery = $"SELECT MAX(StreamSequence) FROM events WHERE StreamID = {Unhex(GetHash(streamId))}";
            var maxGlobalSequenceQuery = "SELECT MAX(GlobalSequence) FROM events";

            using (var cn = DB.CreateConnection())
            {
                cn.Open();
                var tr = cn.BeginTransaction();

                var streamSequence = await DB.ExecuteScalarAsync<int>(maxStreamSequenceQuery, cn);

                if (expectedSequence > 0 && streamSequence != expectedSequence)
                {
                    tr.Rollback();
                    throw new UnexpectedStreamSequenceException();
                }

                var globalSequence = await DB.ExecuteScalarAsync<long>(maxGlobalSequenceQuery, cn);

                globalSequence++;
                streamSequence++;

                foreach (var e in events)
                    e.SetSequences(globalSequence++, streamSequence++);

                var insertSql = CreateInsertSql(streamId, events);

                try
                {
                    await DB.ExecuteNonQueryAsync(insertSql, cn);
                    tr.Commit();
                }
                catch
                {
                    tr.Rollback();
                    throw;
                }
            }
        }

        static readonly string BaseSelect = "SELECT GlobalSequence, EventID, OriginalStreamID, StreamSequence, EventType, UtcTimestamp, Metadata, Payload, PayloadFormat FROM events";

        public async Task ReadAsync(long initialCheckpoint, int maxEvents, Action<IPersistedRawEvent> readCallback, CancellationToken ct)
        {
            var limit = maxEvents < Int32.MaxValue ? " LIMIT " + maxEvents : String.Empty;
            var query = $"{BaseSelect} WHERE GlobalSequence >= {initialCheckpoint} ORDER BY GlobalSequence{limit};";

            using (var reader = await DB.ExecuteReaderAsync(query))
            {
                while (await reader.ReadAsync())
                {
                    var e = ReadPersistedRawEvent(reader);
                    readCallback(e);

                    if (ct.IsCancellationRequested)
                        return;
                }
            }
        }

        public async Task ReadStreamAsync(string streamId, int initialSequence, int maxEvents, Action<IPersistedRawEvent> readCallback, CancellationToken ct)
        {
            var limit = maxEvents < Int32.MaxValue ? " LIMIT " + maxEvents : String.Empty;
            var query = $"{BaseSelect} WHERE StreamID = {Unhex(GetHash(streamId))} AND StreamSequence >= {initialSequence} ORDER BY StreamSequence{limit};";

            using (var reader = await DB.ExecuteReaderAsync(query))
            {
                while (await reader.ReadAsync())
                {
                    var e = ReadPersistedRawEvent(reader);
                    readCallback(e);

                    if (ct.IsCancellationRequested)
                        return;
                }
            }
        }

        #endregion

        #region IProjectionStore

        public async Task WriteProjectionIndexAsync(string projectionStreamId, IReadOnlyCollection<IndexSequenceEntry> entries)
        {
            Contract.Requires(IsHexString(projectionStreamId));

            var maxSequenceQuery = $"SELECT MAX(ProjectionStreamSequence) FROM projectionstreams where ProjectionStreamID = UNHEX('{projectionStreamId}')";

            using (var cn = DB.CreateConnection())
            {
                cn.Open();
                var tr = cn.BeginTransaction();

                var maxSequence = await DB.ExecuteScalarAsync<int>(maxSequenceQuery);

                var filteredEntries = entries.Where(e => e.ProjectionStreamSequence > maxSequence);

                if (!filteredEntries.Any())
                {
                    tr.Rollback();
                    return;
                }

                var sb = new StringBuilder();

                sb.Append("INSERT INTO projectionstreams (ProjectionStreamID, ProjectionStreamSequence, GlobalSequence) VALUES ");

                foreach (var e in entries)
                    sb.AppendFormat("(UNHEX('{0}'), {1}, {2}), ", projectionStreamId, e.ProjectionStreamSequence, e.GlobalSequence);

                sb.Length -= 2;

                var insertSql = sb.ToString();

                try
                {
                    DB.ExecuteNonQuery(insertSql);
                    tr.Commit();
                }
                catch
                {
                    tr.Rollback();
                    throw;
                }
            }
        }

        public async Task WriteProjectionCheckpointAsync(string projectionStreamId, long globalSequence)
        {
            Contract.Requires(IsHexString(projectionStreamId));

            var update = $"UPDATE checkpoints SET LastGlobalSequence = {globalSequence} WHERE ProjectionStreamID = UNHEX('{projectionStreamId}')";

            var affected = await DB.ExecuteNonQueryAsync(update);

            if (affected == 0)
            {
                var insert = $"INSERT INTO checkpoints (ProjectionStreamID, LastGlobalSequence) values (UNHEX('{projectionStreamId}'), {globalSequence});";
                await DB.ExecuteNonQueryAsync(insert);
            }
        }

        public Task<long> ReadProjectionCheckpointAsync(string projectionStreamId)
        {
            Contract.Requires(IsHexString(projectionStreamId));
            return DB.ExecuteScalarAsync<long>($"SELECT LastGlobalSequence FROM checkpoints WHERE ProjectionStreamID = UNHEX('{projectionStreamId}')");
        }

        public Task<int> ReadHighestProjectionStreamSequenceAsync(string projectionStreamId)
        {
            Contract.Requires(IsHexString(projectionStreamId));
            return DB.ExecuteScalarAsync<int>($"SELECT MAX(ProjectionStreamSequence) FROM projectionstreams WHERE ProjectionStreamID = UNHEX('{projectionStreamId}')");
        }

        static readonly string BaseProjectionSelect = @"SELECT e.GlobalSequence, EventID, OriginalStreamID, StreamSequence, EventType, UtcTimestamp, Metadata, Payload, PayloadFormat, p.ProjectionStreamSequence FROM events e INNER JOIN projectionstreams p ON e.GlobalSequence = p.GlobalSequence";

        public async Task ReadIndexedProjectionStreamAsync(string projectionStreamId, int initialSequence, int maxEvents, Action<IProjectionRawEvent> readCallback, CancellationToken ct)
        {
            Contract.Requires(IsHexString(projectionStreamId));
            var limit = maxEvents < Int32.MaxValue ? " LIMIT " + maxEvents : String.Empty;
            var query = $"{BaseProjectionSelect} WHERE ProjectionStreamID = UNHEX('{projectionStreamId}') AND ProjectionStreamSequence >= {initialSequence} ORDER BY StreamSequence{limit};";

            using (var reader = await DB.ExecuteReaderAsync(query))
            {
                while (await reader.ReadAsync())
                {
                    var e = ReadProjectionRawEvent(reader, projectionStreamId);
                    readCallback(e);

                    if (ct.IsCancellationRequested)
                        return;
                }
            }
        }

        #endregion
        
        #region Helpers

        private static IPersistedRawEvent ReadPersistedRawEvent(DbDataReader reader)
        {
            return new PersistedRawEvent
            {
                GlobalSequence = reader.GetInt64(0),
                EventID = reader.GetGuid(1),
                StreamID = reader.GetString(2),
                StreamSequence = reader.GetInt32(3),
                EventType = reader.GetString(4),
                UtcTimestamp = reader.GetDateTime(5),
                Metadata = (byte[])reader[6],
                Payload = (byte[])reader[7],
                PayloadFormat = reader.GetInt32(8)
            };
        }

        private static IProjectionRawEvent ReadProjectionRawEvent(DbDataReader reader, string projectionStreamId)
        {
            return new ProjectionRawEvent
            {
                GlobalSequence = reader.GetInt64(0),
                EventID = reader.GetGuid(1),
                StreamID = reader.GetString(2),
                StreamSequence = reader.GetInt32(3),
                EventType = reader.GetString(4),
                UtcTimestamp = reader.GetDateTime(5),
                Metadata = (byte[])reader[6],
                Payload = (byte[])reader[7],
                PayloadFormat = reader.GetInt32(8),
                ProjectionStreamID = projectionStreamId,
                ProjectionStreamSequence = reader.GetInt32(9)
            };
        }

        private static bool IsHexString(string str)
        {
            foreach (var c in str)
            {
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                    return false;
            }

            return true;
        }

        private static string Escape(string s)
        {
            return MySqlHelper.EscapeString(s);
        }

        private static string Unhex(byte[] bytes)
        {
            if (bytes == null)
                return "NULL";

            var sb = new StringBuilder(bytes.Length * 2 + 9);

            sb.Append("UNHEX('");

            foreach (var b in bytes)
                sb.Append(b.ToString("x2"));

            sb.Append("')");

            return sb.ToString();
        }

        private static string ToDateString(DateTime dt)
        {
            return dt.ToString("yyyy-MM-dd HH:mm:ss.fffffff");
        }

        private static string CreateInsertSql(string streamID, IEnumerable<UnpersistedRawEvent> events)
        {
            var sb = new StringBuilder();

            sb.Append("INSERT INTO events (GlobalSequence, EventID, StreamID, OriginalStreamID, StreamSequence, EventType, UtcTimestamp, Metadata, Payload, PayloadFormat) VALUES ");

            var valueFormat = "({0}, {1}, {2}, '{3}', {4}, '{5}', '{6}', {7}, {8}, {9}), ";

            foreach (var e in events)
            {
                sb.AppendFormat(valueFormat,
                    e.GlobalSequence,
                    Unhex(e.EventID.ToByteArray()),
                    Unhex(GetHash(streamID)),
                    Escape(streamID),
                    e.StreamSequence,
                    Escape(e.EventType),
                    ToDateString(e.UtcTimestamp),
                    Unhex(e.Metadata),
                    Unhex(e.Payload),
                    e.PayloadFormat
                );
            }

            sb.Length -= 2;

            sb.Append(";");

            return sb.ToString();
        }

        private static byte[] GetHash(string input)
        {
            var bytes = Encoding.UTF8.GetBytes(input);
            var sha1 = new SHA1Managed();
            return sha1.ComputeHash(bytes);
        }

        #endregion
    }
}
