using DBHelpers;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even.Persistence.Sql
{
    public class SqlServerProjectionStore : IProjectionStore
    {
        public SqlServerProjectionStore(string connectionString, string tableName, string primaryKey)
        {
            _db = new DBHelper(System.Data.SqlClient.SqlClientFactory.Instance, connectionString);
            _tableName = tableName;
            _primaryKey = primaryKey;
        }

        private DBHelper _db;
        private string _tableName;
        private string _primaryKey;
        private HashSet<string> _columns;

        public async Task InitializeAsync()
        {
            using (var reader = await _db.ExecuteReaderAsync($"select top 0 * from [{_tableName}]"))
            {
                var schema = reader.GetSchemaTable();

                var cols = schema.Rows.Cast<DataRow>().Select(r => (string)r["ColumnName"]);
                _columns = new HashSet<string>(cols, StringComparer.OrdinalIgnoreCase);
            }
        }

        public Task DeleteAllAsync()
        {
            return _db.ExecuteNonQueryAsync($"TRUNCATE TABLE [{_tableName}]");
        }

        public Task DeleteAsync(object key)
        {
            var cmd = _db.CreateCommand($"DELETE FROM [{_tableName}] WHERE [{_primaryKey}] = {{0}}", key);
            return _db.ExecuteNonQueryAsync(cmd);
        }

        public async Task<ProjectionState> GetStateAsync()
        {
            return null;
        }

        public async Task ProjectAsync(object key, object value)
        {
            var values = GetValues(value);

            if (!values.Any())
                return;

            var parameters = values.Select((kvp, i) =>
            {
                var p = _db.Factory.CreateParameter();
                p.ParameterName = "@p" + i;
                p.Value = kvp.Value;
                return new
                {
                    Col = kvp.Key,
                    Parameter = p
                };
            }).ToList();

            var colNames = String.Join(", ", parameters.Select(p => p.Col));
            var paramNames = String.Join(", ", parameters.Select(p => p.Parameter.ParameterName));

            var sb = new StringBuilder();
            sb.AppendLine($"MERGE [{_tableName}] as target USING (values ({paramNames})) as source ({colNames})");
            sb.AppendLine($"ON [{_primaryKey}] = @key");
            sb.Append("WHEN MATCHED THEN UPDATE SET ");

            foreach (var p in parameters)
                sb.Append($"[{p.Col}] = {@p.Parameter.ParameterName}, ");

            sb.Length -= 2;
            sb.AppendLine();
            sb.Append($"WHEN NOT MATCHED THEN INSERT ({_primaryKey}, {colNames}) VALUES (@key, {paramNames});");

            var cmd = _db.CreateCommand(sb.ToString());
            cmd.Parameters.AddRange(parameters.Select(p => p.Parameter).ToArray());
            
            var keyParam = _db.Factory.CreateParameter();
            keyParam.ParameterName = "@key";
            keyParam.Value = key;
            cmd.Parameters.Add(keyParam);

            await _db.ExecuteNonQueryAsync(cmd);
        }

        protected Dictionary<string, object> GetValues(object obj)
        {
            if (obj != null)
            {
                var props = obj.GetType().GetProperties();

                return props.Where(p => _columns.Contains(p.Name)).ToDictionary(p => p.Name, p =>
                {
                    var value = p.GetValue(obj);

                    var tc = Type.GetTypeCode(p.PropertyType);

                    if (tc == TypeCode.Object)
                        value = JsonConvert.SerializeObject(value);

                    return value;
                });
            }

            return new Dictionary<string, object>();
        }
    }
}
