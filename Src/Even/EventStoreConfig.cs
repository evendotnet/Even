using DBHelpers;

namespace Even
{
    public class EventStoreConfig
    {
        public static EventStoreConfig Default { get; } = new EventStoreConfig();

        //public string MySqlConnectionString { get; } = "Server=localhost; Database=eventstore; Uid=eventstore; Pwd=eventstore;";

        //internal DBHelper GetHelper()
        //{
        //    return new DBHelper(MySql.Data.MySqlClient.MySqlClientFactory.Instance, MySqlConnectionString);
        //}
    }
}
