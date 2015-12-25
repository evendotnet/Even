using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DBHelpers;
using System.Data;

namespace Even.Persistence.Sql
{
    /// <summary>
    /// This is a base implementation for sql stores. This is not optimized for performance or highly concurrent scenarios.
    /// Instead, this is intended to be generic and "easy to extend" driver to allow for as many backing stores as possible.
    /// Optimizations may be implemented in specialized by overriding these methods or creating from scratch.
    /// </summary>
    public abstract class BaseSqlStore : IEventStore
    {
        public BaseSqlStore(DbProviderFactory factory, string connectionString, bool createTables)
        {
            DB = new DBHelper(factory, connectionString);

            _createTables = createTables;
        }

        private bool _createTables;

        /// <summary>
        /// Name of the 'Events' table.
        /// </summary>
        public virtual string EventsTable => "Event";

        /// <summary>
        /// Name of the 'ProjectionIndex' table.
        /// </summary>
        public virtual string ProjectionIndexTable => "ProjectionIndex";

        /// <summary>
        /// Name of the 'ProjectionCheckpoint' table.
        /// </summary>
        public virtual string ProjectionCheckpointTable => "ProjectionCheckpoint";

        /// <summary>
        /// Escapes table and field names in the implemented SQL dialect.
        /// </summary>
        protected abstract string EscapeIdentifier(string identifier);

        protected virtual DBHelper DB { get; set; }

        protected abstract string CommandText_CreateTables { get; }
        protected abstract string CommandText_SelectGeneratedGlobalSequence { get; }
            
        protected string CommandText_ClearProjectionIndex { get; set; }
        protected string CommandText_Read { get; set; }
        protected string CommandText_ReadHighestGlobalSequence { get; set; }
        protected string CommandText_ReadHighestIndexedProjectionGlobalSequence { get; set; }
        protected string CommandText_ReadHighestIndexedProjectionStreamSequence { get; set; }
        protected string CommandText_ReadIndexedProjectionStream { get; set; }
        protected string CommandText_ReadProjectionCheckpoint { get; set; }
        protected string CommandText_ReadStream { get; set; }
        protected string CommandText_Write { get; set; }
        protected string CommandText_WriteProjectionCheckpoint_Insert { get; set; }
        protected string CommandText_WriteProjectionCheckpoint_Update { get; set; }
        protected string CommandText_WriteProjectionIndex { get; set; }
        protected string CommandText_WriteStream { get; set; }
        protected string CommandText_WriteStream_Count { get; set; }


        #region Interface Implementation

        public virtual async Task InitializeAsync()
        {
            BuildCommands();

            if (_createTables)
                await CreateTablesAsync();
        }

        public async Task ClearProjectionIndexAsync(Stream stream)
        {
            var command = DB.CreateCommand(CommandText_ClearProjectionIndex, stream.Hash);
            await DB.ExecuteNonQueryAsync(command);
        }

        public async Task ReadAsync(long initialSequence, int count, Action<IPersistedRawEvent> readCallback, CancellationToken ct)
        {
            if (count == 0)
                return;

            var sc = StartCount.From(initialSequence, count);

            var events = await DB.ExecuteListAsync(CommandText_Read, r => ReadEvent(r, null), sc.Start, sc.Count);

            foreach (var e in events)
            {
                if (ct.IsCancellationRequested)
                    break;

                readCallback(e);
            }
        }

        public Task<long> ReadHighestGlobalSequenceAsync()
        {
            return DB.ExecuteScalarAsync<long>(CommandText_ReadHighestGlobalSequence);
        }

        public Task<long> ReadHighestIndexedProjectionGlobalSequenceAsync(Stream stream)
        {
            var command = DB.CreateCommand(CommandText_ReadHighestIndexedProjectionGlobalSequence, stream.Hash);
            return DB.ExecuteScalarAsync<long>(command);
        }

        public Task<int> ReadHighestIndexedProjectionStreamSequenceAsync(Stream stream)
        {
            var command = DB.CreateCommand(CommandText_ReadHighestIndexedProjectionStreamSequence, stream.Hash);
            return DB.ExecuteScalarAsync<int>(command);
        }

        public async Task ReadIndexedProjectionStreamAsync(Stream stream, int initialSequence, int count, Action<IPersistedRawEvent> readCallback, CancellationToken ct)
        {
            if (count == 0)
                return;

            var sc = StartCount.From(initialSequence, count);
            var command = DB.CreateCommand(CommandText_ReadIndexedProjectionStream, stream.Hash);

            var events = await DB.ExecuteListAsync(command, r => ReadEvent(r, stream), sc.Start, sc.Count);

            foreach (var e in events)
            {
                if (ct.IsCancellationRequested)
                    break;

                readCallback(e);
            }
        }

        public Task<long> ReadProjectionCheckpointAsync(Stream stream)
        {
            var command = DB.CreateCommand(CommandText_ReadProjectionCheckpoint, stream.Hash);
            return DB.ExecuteScalarAsync<long>(command);
        }

        public async Task ReadStreamAsync(Stream stream, int initialSequence, int count, Action<IPersistedRawEvent> readCallback, CancellationToken ct)
        {
            if (count == 0)
                return;

            var sc = StartCount.From(initialSequence, count);
            var command = DB.CreateCommand(CommandText_ReadStream, stream.Hash);

            var events = await DB.ExecuteListAsync(command, r => ReadEvent(r, stream), sc.Start, sc.Count);

            foreach (var e in events)
            {
                if (ct.IsCancellationRequested)
                    break;

                readCallback(e);
            }
        }

        public async Task WriteAsync(IReadOnlyCollection<IUnpersistedRawStreamEvent> events)
        {
            var commands = events.Select(CreateWriteStreamCommand).ToList();
            var sequences = new List<long>(commands.Count);
            
            using (var cn = DB.CreateConnection())
            {
                await cn.OpenAsync();
                var tr = cn.BeginTransaction();

                try
                {
                    foreach (var c in commands)
                    {
                        var seq = await DB.ExecuteScalarAsync<long>(c, tr);
                        sequences.Add(seq);
                    }

                    tr.Commit();
                }
                catch (Exception ex)
                {
                    tr.Rollback();
                    HandleWriteException(ex);
                    throw;
                }

                cn.Close();
            }

            var i = 0;

            foreach (var e in events)
                e.GlobalSequence = sequences[i++];
        }

        public async Task WriteProjectionCheckpointAsync(Stream stream, long globalSequence)
        {
            var command = DB.CreateCommand(CommandText_WriteProjectionCheckpoint_Update, stream.Hash, globalSequence);
            var affected = await DB.ExecuteNonQueryAsync(command);

            if (affected == 0)
            {
                command = DB.CreateCommand(CommandText_WriteProjectionCheckpoint_Insert, stream.Hash, globalSequence);
                await DB.ExecuteNonQueryAsync(command);
            }
        }

        public async Task WriteProjectionIndexAsync(Stream stream, int expectedSequence, IReadOnlyCollection<long> globalSequences)
        {
            // WARNING: this sequence check is not actually safe and duplicate exceptions may be thrown even after this,
            // but because indexes are written by a single actor and with a good interval between writes, this shouldn't
            // be a problem for a generic driver. A better approach (like locking the entire table) should be safer
            // but can't be implemented in a generic way. This is left to be optimized in the specialized driver.

            var currentSequence = await ReadHighestIndexedProjectionStreamSequenceAsync(stream);

            if (currentSequence != expectedSequence)
                throw new UnexpectedStreamSequenceException();

            var seq = expectedSequence + 1;

            var commands = globalSequences.Select(gs => DB.CreateCommand(CommandText_WriteProjectionIndex, stream.Hash, seq++, gs)).ToList();

            using (var cn = DB.CreateConnection())
            {
                await cn.OpenAsync();

                var tr = cn.BeginTransaction();

                try
                {
                    foreach (var c in commands)
                        await DB.ExecuteNonQueryAsync(c, tr);

                    tr.Commit();
                }
                catch (Exception ex)
                {
                    tr.Rollback();
                    HandleWriteProjectionIndexException(ex);
                    throw;
                }

                cn.Close();
            }
        }

        public async Task WriteStreamAsync(Stream stream, int expectedSequence, IReadOnlyCollection<IUnpersistedRawEvent> events)
        {
            // WARNING: this check is not actually thread safe and may throw exceptions even after this, 
            // but because writes with expected sequences will normally happen from a single actor, this
            // shouldn't be much of a problem. A better approach can be used using dedicated statements
            // in specialized drivers.

            if (expectedSequence != ExpectedSequence.Any)
            {
                var command = DB.CreateCommand(CommandText_WriteStream_Count, stream.Hash);
                var currentSequence = await DB.ExecuteScalarAsync<long>(command);

                if (currentSequence != expectedSequence)
                    throw new UnexpectedStreamSequenceException();
            }

            var commands = events.Select(e => CreateWriteCommand(stream, e)).ToList();
            var sequences = new List<long>(commands.Count);

            using (var cn = DB.CreateConnection())
            {
                await cn.OpenAsync();
                var tr = cn.BeginTransaction();

                try
                {
                    foreach (var c in commands)
                    {
                        var seq = await DB.ExecuteScalarAsync<long>(c, tr);
                        sequences.Add(seq);
                    }

                    tr.Commit();
                }
                catch (Exception ex)
                {
                    tr.Rollback();
                    HandleWriteStreamException(ex);
                    throw;
                }

                cn.Close();
            }

            var i = 0;

            foreach (var e in events)
                e.GlobalSequence = sequences[i++];
        }

        #endregion

        #region Helpers

        protected virtual void BuildCommands()
        {
            var eventsTable = EscapeIdentifier(EventsTable);
            var projectionTable = EscapeIdentifier(ProjectionIndexTable);
            var checkpointTable = EscapeIdentifier(ProjectionCheckpointTable);
            var selectFields = "GlobalSequence, EventID, StreamHash, StreamName, EventType, UtcTimestamp, Metadata, Payload, PayloadFormat";
            var insertFields = "EventID, StreamHash, StreamName, EventType, UtcTimestamp, Metadata, Payload, PayloadFormat";
            var insertValues = String.Join(", ", insertFields.Split(',').Select((_, i) => "{" + i + "}"));

            // string format markers will be replaces by parameters using the provider format
            CommandText_ClearProjectionIndex = $"DELETE FROM {projectionTable} WHERE ProjectionStreamHash = {{0}}; DELETE FROM {checkpointTable} WHERE ProjectionStreamHash = {{0}}";
            CommandText_Read = $"SELECT {selectFields} FROM {eventsTable}";
            CommandText_ReadHighestGlobalSequence = $"SELECT MAX(GlobalSequence) FROM {eventsTable}";
            CommandText_ReadHighestIndexedProjectionGlobalSequence = $"SELECT MAX(GlobalSequence) FROM {projectionTable} WHERE ProjectionStreamHash = {{0}}";
            CommandText_ReadHighestIndexedProjectionStreamSequence = $"SELECT MAX(ProjectionStreamSequence) FROM {projectionTable} WHERE ProjectionStreamHash = {{0}}";
            CommandText_ReadIndexedProjectionStream = $"SELECT e.{selectFields} FROM {eventsTable} e INNER JOIN {projectionTable} p ON e.GlobalSequence = p.GlobalSequence WHERE p.ProjectionStreamHash = {{0}} ORDER BY p.GlobalSequence";
            CommandText_ReadProjectionCheckpoint = $"SELECT LastGlobalSequence FROM {checkpointTable} WHERE ProjectionStreamHash = {{0}}";
            CommandText_ReadStream = $"SELECT {selectFields} FROM {eventsTable} WHERE StreamHash = {{0}} ORDER BY GlobalSequence";
            CommandText_Write = $"INSERT INTO {eventsTable} ({insertFields}) VALUES ({insertValues}); {CommandText_SelectGeneratedGlobalSequence};";
            CommandText_WriteProjectionCheckpoint_Insert = $"INSERT INTO {checkpointTable} (ProjectionStreamHash, LastGlobalSequence) VALUES({{0}}, {{1}})";
            CommandText_WriteProjectionCheckpoint_Update = $"UPDATE {checkpointTable} SET LastGlobalSequence = {{1}} WHERE ProjectionStreamHash = {{0}}";
            CommandText_WriteProjectionIndex = $"INSERT INTO {projectionTable} (ProjectionStreamHash, ProjectionStreamSequence, GlobalSequence) VALUES ({{0}}, {{1}}, {{2}})";
            CommandText_WriteStream = CommandText_Write;
            CommandText_WriteStream_Count = $"SELECT COUNT(1) FROM {eventsTable} WHERE StreamHash = {{0}}";
        }

        protected virtual IPersistedRawEvent ReadEvent(DbDataReader reader, Stream stream)
        {
            // select order
            // Field: GlobalSequence, EventID, StreamHash, StreamName, EventType, UtcTimestamp, Metadata, Payload, PayloadFormat
            // Index: 0               1        2           3           4          5             6         7        8

            if (stream != null)
                stream = new Stream(stream, reader.GetString(3));
            else
                stream = new Stream((byte[])reader[2], reader.GetString(3));

            return new PersistedRawEvent
            {
                GlobalSequence = reader.GetInt64(0),
                EventID = reader.GetGuid(1),
                Stream = stream,
                EventType = reader.GetString(4),
                UtcTimestamp = reader.GetDateTime(5),
                Metadata = reader.IsDBNull(6) ? null : (byte[])reader[6],
                Payload = (byte[])reader[7],
                PayloadFormat = reader.GetInt32(8)
            };
        }

        protected virtual DbCommand CreateWriteCommand(Stream stream, IUnpersistedRawEvent e)
        {
            // order: EventID, StreamHash, StreamName, EventType, UtcTimestamp, Metadata, Payload, PayloadFormat
            // index: 0        1           2           3          4             5         6        7
            var command = DB.CreateCommand(CommandText_Write, e.EventID, stream.Hash, stream.Name, e.EventType, e.UtcTimestamp, e.Metadata, e.Payload, e.PayloadFormat);

            // dbtype may not be inferred because payload may be null - all other fields are "not null"
            // this avoids issues when the provider generates or converts parameters
            command.Parameters[5].DbType = DbType.Binary;

            return command;
        }

        protected virtual DbCommand CreateWriteStreamCommand(IUnpersistedRawStreamEvent e)
        {
            return CreateWriteCommand(e.Stream, e);
        }

        protected virtual void HandleWriteException(Exception ex)
        {
            HandleException(ex);
        }

        protected virtual void HandleWriteProjectionIndexException(Exception ex)
        {
            HandleException(ex);
        }

        protected virtual void HandleWriteStreamException(Exception ex)
        {
            HandleException(ex);
        }

        protected abstract void HandleException(Exception ex);

        // small helper class to normalize start/count values for queries
        protected class StartCount
        {
            public static StartCount From(long initialSequence, int count)
            {
                if (initialSequence < 1)
                    initialSequence = 1;

                if (count < 0)
                    count = 0;

                return new StartCount
                {
                    Start = (int)initialSequence - 1,
                    Count = count
                };
            }

            public int Start { get; private set; }
            public int Count { get; private set; }
        }

        protected async Task CreateTablesAsync()
        {
            var commandText = String.Format(CommandText_CreateTables,
                EventsTable,
                ProjectionIndexTable,
                ProjectionCheckpointTable
            );

            await DB.ExecuteNonQueryAsync(commandText);
        }

        #endregion
    }
}
