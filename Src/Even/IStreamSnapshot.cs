using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even
{
    public interface IStreamSnapshot
    {
        long Checkpoint { get; }
        string StreamID { get; }
        object GetSnapshot();
    }
}
