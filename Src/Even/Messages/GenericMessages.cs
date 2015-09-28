using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even.Messages
{
    public class CancelRequest : IRequest
    {
        public CancelRequest(Guid requestId)
        {
            this.RequestID = requestId;
        }

        public Guid RequestID { get; }
    }

    public class Cancelled
    {
        public Cancelled(Guid requestId)
        {
            this.RequestID = requestId;
        }

        public Guid RequestID { get; }
    }

    public class Aborted
    {
        public Aborted(Guid requestId, Exception exception)
        {
            this.RequestID = requestId;
            this.Exception = exception;
        }

        public Guid RequestID { get; }
        public Exception Exception { get; private set; }
    }
}
