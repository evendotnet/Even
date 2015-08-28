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
        public IStorageDriver StorageDriver { get; set; }
        public ICryptoService CryptoService { get; set; }
        public IDataSerializer Serializer { get; set; } = new DefaultSerializer();
        public List<Type> Projections { get; } = new List<Type>();
    }
}
