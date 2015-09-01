using System;

namespace Even
{
    internal class AggregateSnapshot : IAggregateSnapshot
    {
        public string StreamID { get; set; }
        public int StreamSequence { get; set; }
        public object State { get; set; }
    }
}