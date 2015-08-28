using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even
{
    public interface IRawStorageEvent
    {
        long Checkpoint { get; }
        Guid EventID { get; }
        string StreamID { get; }
        int StreamSequence { get; }
        string EventName { get; }
        byte[] Headers { get; }
        byte[] Payload { get; }
    }
}
