using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even
{
    public static class SystemClock
    {
        private static Stopwatch _sw = new Stopwatch();
        public static long MonotonicTicks => _sw.Elapsed.Ticks;
        public static DateTimeOffset Now => DateTimeOffset.Now;
    }
}
