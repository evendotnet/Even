using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even.Persistence.Sql
{
    public class MySqlStore : BaseSqlStore
    {
        public MySqlStore(DbProviderFactory factory, string connectionString, bool createTables)
            : base(factory, connectionString, createTables)
        { }

        public MySqlStore(string connectionString, bool createTables = false)
            : this(DbProviderFactories.GetFactory("MySql.Data.MySqlClient"), connectionString, createTables)
        { }

        protected override string CommandText_SelectGeneratedGlobalSequence => "SELECT LAST_INSERT_ID()";

        protected override string CommandText_CreateTables => @"
create table if not exists {0} (
  GlobalSequence bigint not null primary key auto_increment,
  EventID char(36) not null,
  StreamHash binary(20) not null,
  StreamName varchar(200) not null,
  EventType varchar(50) not null,
  UtcTimestamp datetime not null,
  Metadata blob,
  Payload mediumblob not null,
  PayloadFormat int not null,

  index (StreamHash),
  unique index (EventID)
);

create table if not exists {1} (
  ProjectionStreamHash binary(20) not null,
  ProjectionStreamSequence int not null,
  GlobalSequence bigint not null,
  primary key (ProjectionStreamHash, ProjectionStreamSequence),
  unique index (ProjectionStreamHash, GlobalSequence)
);

create table if not exists {2} (
  ProjectionStreamHash binary(20) not null primary key,
  LastGlobalSequence bigint not null
);
";

        protected override string EscapeIdentifier(string identifier)
        {
            return "`" + identifier + "`";
        }

        protected override void HandleException(Exception ex)
        {
            if (ex.Message.IndexOf("Duplicate", StringComparison.OrdinalIgnoreCase) >= 0)
                throw new DuplicatedEntryException();
        }
    }
}
