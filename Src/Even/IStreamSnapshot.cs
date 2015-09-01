using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even
{
    public interface IAggregateSnapshot
    {
        string StreamID { get; }
        int StreamSequence { get; }
        object State { get; }
    }

    public interface IRawAggregateSnapshot
    {
        string StreamID { get; }
        int StreamSequence { get; }
        string ClrType { get; }
        byte[] Payload { get; }
    }
}
