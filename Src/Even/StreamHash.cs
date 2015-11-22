using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Even
{
    [Obsolete]
    public static class StreamHash
    {
        /// <summary>
        /// Determines if the string is a hash string.
        /// </summary>
        public static bool IsStreamHash(string str)
        {
            if (str == null || str.Length != 40)
                return false;

            for (var i = 0; i < 40; i++)
            {
                var c = str[i];

                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                    return false;
            }

            return true;
        }

        public static string AsHashString(string str)
        {
            if (IsStreamHash(str))
                return str;

            var bytes = ComputeHash(str);

            var sb = new StringBuilder(bytes.Length * 2);

            foreach (var b in bytes)
                sb.Append(b.ToString("x2"));

            return sb.ToString();
        }

        public static byte[] AsHashBytes(string str)
        {
            if (IsStreamHash(str))
            {
                var bytes = new byte[20];

                for (var i = 0; i < 20; i++)
                    bytes[i] = Convert.ToByte(str.Substring(i * 2, 2), 16);

                return bytes;
            }

            return ComputeHash(str);
        }

        private static byte[] ComputeHash(string str)
        {
            var bytes = Encoding.UTF8.GetBytes(str);
            var sha1 = new SHA1Managed();
            return sha1.ComputeHash(bytes);
        }
    }
}
