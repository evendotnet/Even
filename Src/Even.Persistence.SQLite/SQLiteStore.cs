using System;
using System.Data.Common;
using System.Data.SQLite;
using Even.Persistence.Sql;

namespace Even.Persistence.SQLite
{
    public class SQLiteStore : BaseSqlStore
    {
        public SQLiteStore(DbProviderFactory factory, string connectionString, bool createTables)
            : base(factory, connectionString, createTables)
        {
        }

        public SQLiteStore(string connectionString, bool createTables = false)
            : this(DbProviderFactories.GetFactory("System.Data.SQLite"), connectionString, createTables)
        {
        }

        protected override string CommandText_CreateTables => @"
CREATE TABLE IF NOT EXISTS {0} (
    GlobalSequence integer primary key autoincrement,
    EventID uniqueidentifier not null unique,
    StreamHash binary(20) not null,
    StreamName varchar(200) not null,
    EventType varchar(50) not null,
    UtcTimestamp datetime not null,
    Metadata blob,
    Payload blob not null,
    PayloadFormat int not null
);

CREATE INDEX IF NOT EXISTS IX_{0}_StreamHash ON {0} (StreamHash);

CREATE TABLE IF NOT EXISTS {1} (
	ProjectionStreamHash binary(20) not null,
	ProjectionStreamSequence int not null,
	GlobalSequence bigint not null,
	primary key (ProjectionStreamHash, ProjectionStreamSequence)
);

CREATE UNIQUE INDEX IF NOT EXISTS UIX_{1}_ProjectionStreamHash_GlobalSequence on {1} (ProjectionStreamHash, GlobalSequence);

CREATE TABLE IF NOT EXISTS {2} (
	  ProjectionStreamHash binary(20) not null primary key,
	  LastGlobalSequence bigint not null
	);
";

        protected override string CommandText_SelectGeneratedGlobalSequence => "SELECT last_insert_rowid()";

        protected override string EscapeIdentifier(string identifier)
        {
            return "[" + identifier + "]";
        }

        protected override void HandleException(Exception ex)
        {
            var sqlEx = ex as SQLiteException;

            if (sqlEx?.ResultCode == SQLiteErrorCode.Constraint)
                throw new DuplicatedEntryException();
        }
    }
}