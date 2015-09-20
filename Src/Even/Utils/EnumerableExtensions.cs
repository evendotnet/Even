using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even.Utils
{
    public static class EnumerableExtensions
    {
        /// <summary>
        /// Returns true if the enumerable contains a values in sequential order, or is empty. Returns false otherwise.
        /// </summary>
        public static bool IsSequential(this IEnumerable<int> enumeration)
        {
            var gotFirst = false;
            int lastValue = 0;

            foreach (var i in enumeration)
            {
                if (!gotFirst)
                {
                    lastValue = i;
                    gotFirst = true;
                    continue;
                }

                if (i != lastValue + 1)
                    return false;

                lastValue = i;
            }

            if (!gotFirst)
                return false;

            return true;
        }
    }
}
