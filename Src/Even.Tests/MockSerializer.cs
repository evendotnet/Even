using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even.Tests
{
    public class MockSerializer : ISerializer
    {
        public object DeserializeEvent(byte[] bytes, int format, Type type)
        {
            return new object();
        }

        public IReadOnlyDictionary<string, object> DeserializeMetadata(byte[] bytes)
        {
            return new Dictionary<string, object>();
        }

        public byte[] SerializeEvent(object domainEvent, int format)
        {
            return new byte[0];
        }

        public byte[] SerializeMetadata(IReadOnlyDictionary<string, object> metadata)
        {
            return new byte[0];
        }
    }
}
