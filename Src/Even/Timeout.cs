using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even
{
    public class Timeout
    {
        public readonly long Ticks;

        public Timeout(long monotonicTicks)
        {
            Ticks = monotonicTicks;
        }

        public bool IsExpired => SystemClock.MonotonicTicks >= Ticks;

        public static Timeout In(int milliseconds)
        {
            return new Timeout(SystemClock.MonotonicTicks + TimeSpan.FromMilliseconds(milliseconds).Ticks);
        }

        public static Timeout In(TimeSpan timeSpan)
        {
            return new Timeout(SystemClock.MonotonicTicks + timeSpan.Ticks);
        }
    }
}
