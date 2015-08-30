using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even.Messages
{
    public class ProjectionSubscriptionRequest
    {
        public ProjectionQuery Query { get; set; }
        public int LastKnownSequence { get; set; }
        public Guid ReplayID { get; set; }
    }
}
