using DBHelpers;
using Even.Persistence;
using MySql.Data.MySqlClient;

namespace Even.Tests.Persistence
{
    public class MySqlEventStoreTests : EventStoreTests
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
truncate table {dbName}.`{store.EventsTable}`;
truncate table {dbName}.`{store.ProjectionIndexTable}`;
truncate table {dbName}{store.ProjectionCheckpointTable}`;";

            db.ExecuteNonQueryAsync(truncateSql);

            return store;
        }
    }
}
