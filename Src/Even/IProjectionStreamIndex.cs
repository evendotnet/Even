using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even
{
    public interface IProjectionStreamIndex
    {
        string ProjectionID { get; }
        int ProjectionSequence { get; }
        long Checkpoint { get; }
    }
}
