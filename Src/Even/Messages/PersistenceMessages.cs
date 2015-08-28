using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even.Messages
{
    public abstract class PersistenceMessage
    {
        public Guid EventID { get; set; }
    }

    public class PersistenceRequest : PersistenceMessage
    {
        public string StreamID { get; set; }
        public int StreamSequence { get; set; }
        public object DomainEvent { get; set; }
    }

    public class PersistenceSuccessful : PersistenceMessage
    { }

    public class PersistenceFailed : PersistenceMessage
    { }
}
