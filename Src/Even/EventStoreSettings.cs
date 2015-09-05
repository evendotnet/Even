using Akka.Actor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even
{
    public class EventStoreSettings
    {
        public string EventStoreID { get; set; }
        public IStreamStore Store { get; set; }
        public ISerializer Serializer { get; set; } = new DefaultSerializer();
    }
}
