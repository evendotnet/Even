using System.Data.Common;
using DBHelpers;
using Even.Persistence.SQLite;

namespace Even.Tests.Persistence
{
#if SQLITE
    public
#endif
    class SQLiteEventStoreTests : EventStoreTests
    {
        private string ConnectionString = "Data Source=event_test.db;Version=3;New=True;";
        protected override IEventStore CreateStore()
        {
            return new SQLiteStore(ConnectionString, true);
        }

        protected override void ResetStore()
        {
            var db = new DBHelper(DbProviderFactories.GetFactory("System.Data.SQLite"), ConnectionString);

            var store = (SQLiteStore)Store;

            var a = store.EventsTable;
            var b = store.ProjectionIndexTable;
            var c = store.ProjectionCheckpointTable;

            db.ExecuteNonQuery($"DELETE FROM {a}; DELETE FROM SQLITE_SEQUENCE WHERE NAME='{a}'; DELETE FROM {b}; DELETE FROM SQLITE_SEQUENCE WHERE NAME='{b}'; DELETE FROM {c}; DELETE FROM SQLITE_SEQUENCE WHERE NAME='{c}';");
        }
    }
}
