using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even.Persistence
{
    public class MySqlStore : BaseSqlStore
    {
        public MySqlStore(DbProviderFactory providerFactory, string connectionString, string tablePrefix)
            : base(providerFactory, connectionString, tablePrefix)
        { }

        protected override string MaxLimitValue => "18446744073709551615";

        protected override string SqlFormat_Initialization => @"
create table if not exists `{0}` (
  GlobalSequence bigint not null primary key,
  EventID binary(16) not null,
  StreamID binary(20) not null,
  OriginalStreamID varchar(200) not null,
  EventType varchar(50) not null,
  UtcTimestamp datetime not null,
  Metadata blob,
  Payload mediumblob not null,
  PayloadFormat int not null,

  index (StreamID),
  unique index (EventID)
);

create table if not exists `{1}` (
  ProjectionStreamID binary(20) not null,
  ProjectionStreamSequence int not null,
  GlobalSequence bigint not null,
  primary key (ProjectionStreamID, ProjectionStreamSequence),
  unique index (ProjectionStreamID, GlobalSequence)
);

create table if not exists `{2}` (
  ProjectionStreamID binary(20) not null primary key,
  LastGlobalSequence bigint not null
);
";

        protected override string EscapeIdentifier(string tableName)
        {
            return "`" + tableName + "`";
        }

        static char[] CharsToEscape = new[] { '\'', '\\' };

        protected override string EscapeString(string str)
        {
            return str.Replace(@"\", @"\\").Replace("'", @"\'");
        }

        protected override string Format(Guid guid)
        {
            return Format(guid.ToByteArray());
        }

        protected override string Format(byte[] bytes)
        {
            if (bytes == null)
                return "NULL";

            return FormatBytesInternal(bytes, "UNHEX('", "')");
        }

        protected override string Format(DateTime dt)
        {
            return dt.ToString(@"\'yyyy-MM-dd HH:mm:ss.ffffff\'");
        }

        protected override void HandleInsertException(Exception ex)
        {
            if (ex.Message.IndexOf("duplicate", StringComparison.InvariantCultureIgnoreCase) >= 0)
                throw new DuplicatedEventException();
        }
    }
}
