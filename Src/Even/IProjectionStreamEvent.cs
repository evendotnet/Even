using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even
{
    public interface IProjectionStreamEvent
    {
        string QueryID { get; }
        int Sequence { get; }
        int SequenceHash { get; }
        IStreamEvent Event { get; }
    }
}
