using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even
{
    public interface IStreamSnapshot
    {
        string StreamID { get; }
        int StreamSequence { get; }
        object State { get; }
    }

    public interface IRawAggregateSnapshot
    {
        string StreamID { get; }
        int StreamSequence { get; }
        byte[] Payload { get; }
    }
}
