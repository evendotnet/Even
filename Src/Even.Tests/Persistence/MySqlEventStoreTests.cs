using DBHelpers;
using Even.Persistence;
using MySql.Data.MySqlClient;

namespace Even.Tests.Persistence
{
    #if MYSQL
    public
    #endif
    class MySqlEventStoreTests : EventStoreTests
    {
        protected override IEventStore InitializeStore()
        {
            var dbName = "even_test";
            var cns = "Server=localhost;Uid=even_test;Pwd=even_test;";
            var db = new DBHelper(MySqlClientFactory.Instance, cns);

            db.ExecuteNonQuery("CREATE DATABASE IF NOT EXISTS " + dbName);

            var store = new MySqlStore(MySqlClientFactory.Instance, cns + "Database=" + dbName, null);
            store.InitializeStore();

            var truncateSql = $@"
DELETE FROM {dbName}.`{store.EventsTable}`;
DELETE FROM {dbName}.`{store.ProjectionIndexTable}`;
DELETE FROM {dbName}.`{store.ProjectionCheckpointTable}`;";

            db.ExecuteNonQuery(truncateSql);

            return store;
        }
    }
}
