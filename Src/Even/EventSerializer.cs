using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even
{
    public interface IDataSerializer
    {
        byte[] Serialize(object o);
        object Deserialize(Type type, byte[] data);
        Dictionary<string, object> Deserialize(byte[] data);
    }

    public class DefaultSerializer : IDataSerializer
    {
        public byte[] Serialize(object o)
        {
            if (o == null)
                return null;

            using (var ms = new MemoryStream())
            using (var writer = new BsonWriter(ms))
            {
                var serializer = new JsonSerializer();
                serializer.Serialize(writer, o);
                return ms.ToArray();
            }
        }

        public object Deserialize(Type type, byte[] data)
        {
            if (data == null)
                return null;

            using (var ms = new MemoryStream(data))
            using (var reader = new BsonReader(ms))
            {
                var serializer = new JsonSerializer();
                return serializer.Deserialize(reader, type);
            }
        }

        public Dictionary<string, object> Deserialize(byte[] data)
        {
            if (data == null)
                return null;

            using (var ms = new MemoryStream(data))
            using (var reader = new BsonReader(ms))
            {
                var serializer = new JsonSerializer();
                return serializer.Deserialize<Dictionary<string, object>>(reader);
            }
        }
    }
}
