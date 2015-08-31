using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even.Messages
{
    public class StartProjection
    {
        public string ProjectionID { get; set; }
        public Type ProjectionType { get; set; }
    }
}
