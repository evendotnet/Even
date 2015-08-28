using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even
{
    public class ProjectionState
    {
        public int Sequence { get; set; }
        public int Checkpoint { get; set; }
        public int Hash { get; set; }
    }
}
