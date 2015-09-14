using DBHelpers;
using Even.Persistence;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even.Tests.Persistence
{
    public class SqlServer2012StoreTests : EventStoreTests
    {
        protected override IEventStore InitializeStore()
        {
            var dbName = "even_test";
            var cns = "Server=localhost\\sqlexpress; Trusted_Connection=True;";
            var db = new DBHelper(SqlClientFactory.Instance, cns);

            db.ExecuteNonQuery($"if db_id('{dbName}') is null create database {dbName};");

            var store = new SqlServer2012Store(cns + "Database=" + dbName, null);
            store.InitializeStore();

            var truncateSql = $@"
use {dbName};
DELETE FROM [{store.EventsTable}];
DELETE FROM [{store.ProjectionIndexTable}];
DELETE FROM [{store.ProjectionCheckpointTable}];";

            db.ExecuteNonQuery(truncateSql);

            return store;
        }
    }
}
