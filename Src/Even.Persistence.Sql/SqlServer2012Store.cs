using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even.Persistence.Sql
{
    public class SqlServer2012Store : BaseSqlStore
    {
        public SqlServer2012Store(DbProviderFactory factory, string connectionString, bool createTables)
            : base(factory, connectionString, createTables)
        { }

        public SqlServer2012Store(string connectionString, bool createTables = false)
            : this(System.Data.SqlClient.SqlClientFactory.Instance, connectionString, createTables)
        { }

        protected override string CommandText_SelectGeneratedGlobalSequence => "SELECT SCOPE_IDENTITY()";

        protected override string CommandText_CreateTables => @"
if not exists (select * from information_schema.tables where table_name = N'{0}')
begin
	create table [{0}] (
	  GlobalSequence bigint not null primary key identity,
	  EventID uniqueidentifier not null unique,
	  StreamHash binary(20) not null,
	  StreamName varchar(200) not null,
	  EventType varchar(50) not null,
	  UtcTimestamp datetime not null,
	  Metadata varbinary(max),
	  Payload varbinary(max) not null,
	  PayloadFormat int not null,

	  index [IX_{0}_StreamHash] (StreamHash)
	);
end;

if not exists (select * from information_schema.tables where table_name = N'{1}')
begin
	create table [{1}] (
	  ProjectionStreamHash binary(20) not null,
	  ProjectionStreamSequence int not null,
	  GlobalSequence bigint not null,
	  primary key (ProjectionStreamHash, ProjectionStreamSequence)
	);

    create unique index [UIX_{1}_ProjectionStreamHash_GlobalSequence] on [{1}] (ProjectionStreamHash, GlobalSequence);
end;

if not exists (select * from information_schema.tables where table_name = N'{2}')
begin
	create table [{2}] (
	  ProjectionStreamHash binary(20) not null primary key,
	  LastGlobalSequence bigint not null
	);
end;
";

        protected override string EscapeIdentifier(string identifier)
        {
            return "[" + identifier + "]";
        }

        protected override void HandleException(Exception ex)
        {
            var sqlEx = ex as SqlException;

            if (sqlEx?.Number == 2627 || ex.Message.Contains("duplicate"))
                throw new DuplicatedEntryException();
        }
    }
}
