using DBHelpers;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics.Contracts;

namespace Even.Persistence
{
    public abstract class BaseSqlStore : IEventStore
    {
        public BaseSqlStore(DbProviderFactory providerFactory, string connectionString, string tablePrefix)
        {
            DB = new DBHelper(providerFactory, connectionString);

            EventsTable = EscapeIdentifier(tablePrefix + "Events");
            ProjectionIndexTable = EscapeIdentifier(tablePrefix + "ProjectionIndex");
            ProjectionCheckpointTable = EscapeIdentifier(tablePrefix + "ProjectionCheckpoint");
        }

        DBHelper DB;

        public async Task WriteEventsAsync(IReadOnlyCollection<UnpersistedRawEvent> events)
        {
            var globalSequenceQuery = String.Format(SqlFormat_SelectMaxGlobalSequence, EventsTable);

            using (var cn = DB.CreateConnection())
            {
                await cn.OpenAsync();

                var tr = cn.BeginTransaction();

                var globalSequence = await DB.ExecuteScalarAsync<long>(globalSequenceQuery, cn);

                var batches = BuildInsertEventBatches(globalSequence + 1, events);

                try
                {
                    foreach (var sql in batches)
                        await DB.ExecuteNonQueryAsync(sql, cn);

                    tr.Commit();
                }
                catch
                {
                    tr.Rollback();
                    throw;
                }

                // update the sequences 
                var sequence = globalSequence + 1;

                foreach (var e in events)
                    e.SetGlobalSequence(sequence++);
            }
        }

        public async Task WriteEventsAsync(string streamId, int expectedSequence, IReadOnlyCollection<UnpersistedRawEvent> events)
        {
            var streamHash = Format(StreamHash.AsHashBytes(streamId));
            var streamCountQuery = String.Format(SqlFormat_SelectStreamCount, EventsTable, streamHash);
            var globalSequenceQuery = String.Format(SqlFormat_SelectMaxGlobalSequence, EventsTable);

            using (var cn = DB.CreateConnection())
            {
                await cn.OpenAsync();

                var tr = cn.BeginTransaction();

                if (expectedSequence >= 0)
                {
                    // since the table doesn't keep track of sequences,
                    // the current sequence = # of records in the stream
                    var streamCount = await DB.ExecuteScalarAsync<int>(streamCountQuery, cn);

                    if (streamCount != expectedSequence)
                    {
                        tr.Rollback();
                        throw new UnexpectedStreamSequenceException();
                    }
                }

                var globalSequence = await DB.ExecuteScalarAsync<long>(globalSequenceQuery, cn);

                var batches = BuildInsertEventBatches(globalSequence + 1, events, streamHash);

                try
                {
                    foreach (var sql in batches)
                        await DB.ExecuteNonQueryAsync(sql, cn);

                    tr.Commit();
                }
                catch
                {
                    tr.Rollback();
                    throw;
                }

                
                var sequence = globalSequence + 1;

                foreach (var e in events)
                    e.SetGlobalSequence(sequence++);
            }
        }

        public async Task WriteProjectionIndexAsync(string streamId, IReadOnlyCollection<long> globalSequences)
        {
            var streamHash = Format(StreamHash.AsHashBytes(streamId));
            var maxIndexSequenceQuery = String.Format(SqlFormat_SelectMaxIndexedGlobalSequence, ProjectionIndexTable, streamHash);

            using (var cn = DB.CreateConnection())
            {
                await cn.OpenAsync();

                var tr = cn.BeginTransaction();

                var lastGlobalSequence = await DB.ExecuteScalarAsync<long>(maxIndexSequenceQuery, cn);
                var newEntries = globalSequences.Where(s => s > lastGlobalSequence);

                var batches = BatchStringBuilder.Build(globalSequences, MaxIndexBatchCount, MaxIndexBatchLength,
                    sb => sb.AppendFormat(SqlFormat_InsertIndexPrefix, ProjectionIndexTable),
                    (sb, s) => sb.AppendFormat(SqlFormat_InsertIndexValues, streamHash, s),
                    sb => sb.Length -= 2
                );

                try
                {
                    foreach (var sql in batches)
                        await DB.ExecuteNonQueryAsync(sql, cn);

                    tr.Commit();
                }
                catch (Exception ex)
                {
                    tr.Rollback();
                    throw;
                }
            }
        }

        public async Task WriteProjectionCheckpointAsync(string streamId, long globalSequence)
        {
            var streamHash = Format(StreamHash.AsHashBytes(streamId));
            var update = String.Format(SqlFormat_UpdateCheckpoint, ProjectionCheckpointTable, streamHash, globalSequence);

            var affected = await DB.ExecuteNonQueryAsync(update);

            if (affected == 0)
            {
                var insert = String.Format(SqlFormat_InsertCheckpoint, ProjectionCheckpointTable, streamHash, globalSequence);
                await DB.ExecuteNonQueryAsync(insert);
            }
        }

        public async Task ReadAsync(long initialSequence, int maxEvents, Action<IPersistedRawEvent> readCallback, CancellationToken ct)
        {
            var query = BuildReadQuery(initialSequence, maxEvents);

            using (var reader = await DB.ExecuteReaderAsync(query))
            {
                while (await reader.ReadAsync())
                {
                    if (ct.IsCancellationRequested)
                        break;

                    var @event = ReadEvent(reader, null);
                    readCallback(@event);
                }
            }
        }

        public async Task ReadStreamAsync(string streamId, int initialSequence, int maxEvents, Action<IPersistedRawEvent> readCallback, CancellationToken ct)
        {
            var streamHash = StreamHash.AsHashBytes(streamId);
            var query = BuildReadStreamsQuery(streamHash, initialSequence, maxEvents);

            using (var reader = await DB.ExecuteReaderAsync(query))
            {
                while (await reader.ReadAsync())
                {
                    if (ct.IsCancellationRequested)
                        break;

                    var @event = ReadEvent(reader, streamId);
                    readCallback(@event);
                }
            }
        }

        public async Task ReadIndexedProjectionStreamAsync(string streamId, int initialSequence, int maxEvents, Action<IPersistedRawEvent> readCallback, CancellationToken ct)
        {
            var streamHash = StreamHash.AsHashBytes(streamId);
            var query = BuildReadIndexedProjectionStreamQuery(streamHash, initialSequence, maxEvents);

            using (var reader = await DB.ExecuteReaderAsync(query))
            {
                while (await reader.ReadAsync())
                {
                    if (ct.IsCancellationRequested)
                        break;

                    var @event = ReadEvent(reader, streamId);
                    readCallback(@event);
                }
            }
        }

        public Task<long> ReadProjectionCheckpointAsync(string streamId)
        {
            var streamHash = Format(StreamHash.AsHashBytes(streamId));
            var query = String.Format(SqlFormat_SelectCheckpoint, ProjectionCheckpointTable, streamHash);

            return DB.ExecuteScalarAsync<long>(query);
        }

        public Task<long> ReadHighestProjectionGlobalSequenceAsync(string streamId)
        {
            var streamHash = Format(StreamHash.AsHashBytes(streamId));
            var query = String.Format(SqlFormat_SelectMaxIndexedGlobalSequence, ProjectionIndexTable, streamHash);

            return DB.ExecuteScalarAsync<long>(query);
        }

        public Task<int> ReadHighestProjectionStreamSequenceAsync(string streamId)
        {
            var streamHash = Format(StreamHash.AsHashBytes(streamId));
            var query = String.Format(SqlFormat_SelectCountProjectionStream, ProjectionIndexTable, streamHash);

            return DB.ExecuteScalarAsync<int>(query);
        }

        private IEnumerable<string> BuildInsertEventBatches(long initialSequence, IEnumerable<UnpersistedRawEvent> events, string streamHash = null)
        {
            var batches = BatchStringBuilder.Build<UnpersistedRawEvent>(events,
                MaxEventBatchCount,
                MaxEventBatchLength,
                sb => sb.AppendFormat(SqlFormat_InsertEventPrefix, EventsTable),
                (sb, e) =>
                {
                    sb.AppendFormat(SqlFormat_InsertEventValues,
                        initialSequence++,
                        Format(e.EventID),
                        streamHash ?? Format(StreamHash.AsHashBytes(e.StreamID)),
                        EscapeString(e.StreamID),
                        EscapeString(e.EventType),
                        Format(e.UtcTimestamp),
                        Format(e.Metadata),
                        Format(e.Payload),
                        e.PayloadFormat
                    );
                },
                sb => sb.Length -= 2
            );
            return batches;
        }

        protected virtual IPersistedRawEvent ReadEvent(DbDataReader reader, string streamId)
        {
            return new PersistedRawEvent
            {
                GlobalSequence = reader.GetInt64(0),
                EventID = reader.GetGuid(1),
                StreamID = streamId ?? reader.GetString(2),
                EventType = reader.GetString(3),
                UtcTimestamp = reader.GetDateTime(4),
                Metadata = reader.IsDBNull(5) ? null : (byte[])reader[5],
                Payload = (byte[])reader[6],
                PayloadFormat = reader.GetInt32(7)
            };
        }

        protected abstract string EscapeIdentifier(string tableName);
        protected abstract string EscapeString(string str);
        protected abstract string Format(DateTime dt);
        protected abstract string Format(byte[] bytes);
        protected abstract string Format(Guid guid);
        protected abstract string MaxLimitValue { get; }

        protected virtual int MaxEventBatchCount { get; } = 50;
        protected virtual int MaxEventBatchLength { get; } = 262144; // 256kb

        protected virtual int MaxIndexBatchCount { get; } = 1000;
        protected virtual int MaxIndexBatchLength { get; } = 262144; // 256kb

        protected virtual string SqlFormat_InsertEventPrefix { get; } = "INSERT INTO {0} (GlobalSequence, EventID, StreamID, OriginalStreamID, EventType, UtcTimestamp, Metadata, Payload, PayloadFormat) VALUES ";
        protected virtual string SqlFormat_InsertEventValues { get; } = "({0}, {1}, {2}, '{3}', '{4}', {5}, {6}, {7}, {8}), ";
        protected virtual string SqlFormat_SelectMaxGlobalSequence { get; } = "SELECT MAX(GlobalSequence) FROM {0}";
        protected virtual string SqlFormat_SelectStreamCount { get; } = "SELECT COUNT(*) FROM {0} WHERE StreamID = {1}";

        protected virtual string SqlFormat_InsertIndexPrefix { get; } = "INSERT INTO {0} (ProjectionStreamID, GlobalSequence) VALUES ";
        protected virtual string SqlFormat_InsertIndexValues { get; } = "({0}, {1}), ";
        protected virtual string SqlFormat_SelectMaxIndexedGlobalSequence { get; } = "SELECT MAX(GlobalSequence) FROM {0} WHERE ProjectionStreamID = {1}";
        protected virtual string SqlFormat_SelectCountProjectionStream { get; } = "SELECT COUNT(*) FROM {0} WHERE ProjectionStreamID = {1}";

        protected virtual string SqlFormat_InsertCheckpoint { get; } = "INSERT INTO {0} (StreamID, LastGlobalSequence) VALUES({1}, {2})";
        protected virtual string SqlFormat_UpdateCheckpoint { get; } = "UPDATE {0} SET LastGlobalSequence = {2} WHERE ProjectionStreamID = {1}";
        protected virtual string SqlFormat_SelectCheckpoint { get; } = "SELECT LastGlobalSequence FROM {0} WHERE ProjectionStreamID = {1}";


        protected string SelectFields = "GlobalSequence, EventID, OriginalStreamID, EventType, UtcTimestamp, Metadata, Payload, PayloadFormat";

        protected virtual string BuildReadQuery(long initialSequence, int maxEvents)
        {
            var limit = (maxEvents > 0) ? " LIMIT " + maxEvents : String.Empty;
            var query = $"SELECT {SelectFields} FROM {EventsTable} WHERE GlobalSequence >= {initialSequence} ORDER BY GlobalSequence{limit}";

            return query;
        }

        protected virtual string BuildReadStreamsQuery(byte[] streamId, int initialSequence, int maxEvents)
        {
            var limit = BuildLimitClause(initialSequence, maxEvents);
            var query = $"SELECT {SelectFields} FROM {EventsTable} WHERE StreamID = {Format(streamId)} ORDER BY GlobalSequence{limit}";

            return query;
        }

        protected virtual string BuildReadIndexedProjectionStreamQuery(byte[] streamId, int initialSequence, int maxEvents)
        {
            var limit = BuildLimitClause(initialSequence, maxEvents);
            var query = $"SELECT e.{SelectFields} FROM {EventsTable} e INNER JOIN {ProjectionIndexTable} p ON e.GlobalSequence = p.GlobalSequence WHERE p.ProjectionStreamID = {Format(streamId)} ORDER BY p.GlobalSequence{limit}";

            return query;
        }

        protected virtual string BuildLimitClause(int initialSequence, int maxEvents)
        {
            var limit = maxEvents > 0 ? " LIMIT " + maxEvents : null;
            var offset = initialSequence > 1 ? " OFFSET " + (initialSequence - 1) : null;

            if (limit != null && offset != null)
                return limit + offset;

            if (limit != null && offset == null)
                return limit;

            if (offset != null)
                return " LIMIT " + MaxLimitValue + offset;

            return null;
        }

        protected string EventsTable { get; }
        protected string ProjectionIndexTable { get; }
        protected string ProjectionCheckpointTable { get; }

        protected string FormatBytesInternal(byte[] bytes, string prefix, string suffix)
        {
            if (bytes == null)
                return null;

            var len = (bytes.Length * 2) + (prefix?.Length ?? 0) + (suffix?.Length ?? 0);

            var sb = new StringBuilder(len);

            sb.Append(prefix);

            foreach (var b in bytes)
                sb.Append(b.ToString("x2"));

            sb.Append(suffix);

            return sb.ToString();
        }
    }
}
