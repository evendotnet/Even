using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even
{
    public static class ExpectedSequence
    {
        /// <summary>
        /// The writer will ensure the stream doesn't have any events.
        /// </summary>
        public const int None = 0;

        /// <summary>
        /// No checks will be made by the writer and all events will be appended to the end of the stream.
        /// </summary>
        public const int Any = -1;
    }
}
