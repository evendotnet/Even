using DBHelpers;
using Even.Persistence;
using Even.Persistence.Sql;
using MySql.Data.MySqlClient;
using System.Data.Common;
using System;
using System.Threading.Tasks;

namespace Even.Tests.Persistence
{
#if MYSQL
    public
#endif
    class MySqlEventStoreTests : EventStoreTests
    {
        private string ConnectionString = "Server=localhost; Uid=even_test; Pwd=even_test; Database=even_test;";

        protected override IEventStore CreateStore()
        {
            return new MySqlStore(ConnectionString, true);
        }

        protected override void ResetStore()
        {
            var db = new DBHelper(DbProviderFactories.GetFactory("MySql.Data.MySqlClient"), ConnectionString);

            var store = (MySqlStore)Store;

            var a = store.EventsTable;
            var b = store.ProjectionIndexTable;
            var c = store.ProjectionCheckpointTable;

            db.ExecuteNonQuery($"TRUNCATE TABLE `{a}`; TRUNCATE TABLE `{b}`; TRUNCATE TABLE `{c}`;");
        }
    }
}
