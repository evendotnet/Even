//using System;
//using System.Collections.Generic;
//using System.Data.SqlClient;
//using System.Linq;
//using System.Runtime.ExceptionServices;
//using System.Text;
//using System.Threading.Tasks;

//namespace Even.Persistence
//{
//    public class SqlServer2012Store : BaseSqlStore
//    {
//        public SqlServer2012Store(string connectionString, string tablePrefix = null)
//            : base(System.Data.SqlClient.SqlClientFactory.Instance, connectionString, tablePrefix)
//        { }

//        protected override string MaxLimitValue => null;

//        protected override string SqlFormat_Initialization => @"
//IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = N'{0}')
//BEGIN
//	create table [{0}] (
//	  GlobalSequence bigint not null primary key,
//	  EventID uniqueidentifier not null,
//	  StreamID binary(20) not null,
//	  OriginalStreamID varchar(200) not null,
//	  EventType varchar(50) not null,
//	  UtcTimestamp datetime2 not null,
//	  Metadata varbinary(max),
//	  Payload varbinary(max) not null,
//	  PayloadFormat int not null
//	);

//	create index [IX_{0}_StreamID] on [{0}](StreamID);
//	create unique index [UIX_{0}_EventID] on [{0}](EventID);

//END;

//IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = N'{1}')
//BEGIN
//	create table [{1}] (
//	  ProjectionStreamID binary(20) not null,
//	  ProjectionStreamSequence int not null,
//	  GlobalSequence bigint not null,
//	  primary key (ProjectionStreamID, ProjectionStreamSequence)
//	);

//	create unique index [UIX_{1}] on [{1}](ProjectionStreamID, GlobalSequence)
//END;

//IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = N'{2}')
//BEGIN
//	create table [{2}] (
//	  ProjectionStreamID binary(20) not null primary key,
//	  LastGlobalSequence bigint not null
//	);
//END;
//";

//        protected override string EscapeIdentifier(string tableName)
//        {
//            return "[" + tableName + "]";
//        }

//        protected override string EscapeString(string str)
//        {
//            return str.Replace("'", "''");
//        }

//        protected override string Format(Guid guid)
//        {
//            return Format(guid.ToByteArray());
//        }

//        protected override string Format(byte[] bytes)
//        {
//            if (bytes == null)
//                return "NULL";

//            return FormatBytesInternal(bytes, "0x", null);
//        }

//        protected override string Format(DateTime dt)
//        {
//            return dt.ToString(@"\'yyyy-MM-ddTHH:mm:ss.ffffff\'");
//        }

//        protected override void HandleInsertException(Exception ex)
//        {
//            var sqlEx = ex as SqlException;

//            if (sqlEx != null)
//            {
//                if (sqlEx.Number == 2601)
//                    throw new DuplicatedEntryException(sqlEx);
//            }
//        }

//        protected override string BuildReadQuery(long start, int count)
//        {
//            if (count == 0)
//                return $"SELECT TOP 0 1 FROM {EventsTableEscaped}";

//            return base.BuildReadQuery(start, count);
//        }

//        protected override string BuildReadStreamsQuery(byte[] streamId, int start, int count)
//        {
//            if (count == 0)
//                return $"SELECT TOP 0 1 FROM {EventsTableEscaped}";

//            return base.BuildReadStreamsQuery(streamId, start, count);
//        }

//        protected override string BuildReadIndexedProjectionStreamQuery(byte[] streamId, int start, int count)
//        {
//            if (count == 0)
//                return $"SELECT TOP 0 1 FROM {ProjectionIndexTableEscaped}";

//            return base.BuildReadIndexedProjectionStreamQuery(streamId, start, count);
//        }

//        protected override string BuildLimitClause(long start, int count)
//        {
//            if (count >= 0)
//                return $" OFFSET {start} ROWS FETCH NEXT {count} ROWS ONLY";
//            if (start > 0)
//                return $" OFFSET {start} ROWS";

//            return null;
//        }
//    }
//}
