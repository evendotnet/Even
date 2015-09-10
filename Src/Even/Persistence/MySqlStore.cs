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
            return FormatBytesInternal(bytes, "UNHEX('", "')");
        }

        protected override string Format(DateTime dt)
        {
            return dt.ToString(@"\'yyyy-MM-dd HH:mm:ss.ffffff\'");
        }
    }
}
