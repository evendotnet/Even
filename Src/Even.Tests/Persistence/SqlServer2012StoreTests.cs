using DBHelpers;
using Even.Persistence;
using Even.Persistence.Sql;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even.Tests.Persistence
{
#if SQL2012
    public
#endif
    class SqlServer2012StoreTests : EventStoreTests
    {
        private string ConnectionString = "Server=localhost\\sqlexpress; Trusted_Connection=True; Database=even_test;";

        protected override IEventStore CreateStore()
        {
            return new SqlServer2012Store(ConnectionString, true);
        }

        protected override void ResetStore()
        {
            var db = new DBHelper(SqlClientFactory.Instance, ConnectionString);

            var store = (BaseSqlStore)Store;

            var a = store.EventsTable;
            var b = store.ProjectionIndexTable;
            var c = store.ProjectionCheckpointTable;

            db.ExecuteNonQuery($"TRUNCATE TABLE [{a}]; TRUNCATE TABLE [{b}]; TRUNCATE TABLE [{c}];");
        }
    }
}
