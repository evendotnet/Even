using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even
{
    internal static class Argument
    {
        public static void Requires(bool condition, string paramName = null, string message = null)
        {
            Requires<ArgumentException>(condition, paramName, message);
        }

        public static void Requires<TException>(bool condition, string paramName)
            where TException : Exception, new()
        {
            Requires<TException>(condition, paramName, null);
        }

        public static void Requires<TException>(bool condition, string paramName, string message)
            where TException : Exception, new()
        {
            if (!condition)
            {
                if (typeof(TException) == typeof(ArgumentNullException))
                    throw new ArgumentNullException(paramName);

                if (typeof(TException) == typeof(ArgumentOutOfRangeException))
                    throw new ArgumentOutOfRangeException(paramName);

                if (typeof(TException) == typeof(ArgumentException))
                    throw new ArgumentException(message, paramName);

                throw new TException();
            }
        }
    }
}
