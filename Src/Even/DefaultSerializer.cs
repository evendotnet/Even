using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using System;
using System.Collections.Generic;
using System.IO;

namespace Even
{
    public class DefaultSerializer : ISerializer
    {
        public virtual object DeserializeEvent(byte[] bytes, int format, Type type)
        {
            return DeserializeInternal(bytes, type);
        }

        public virtual IReadOnlyDictionary<string, object> DeserializeMetadata(byte[] bytes)
        {
            return (IReadOnlyDictionary<string, object>)DeserializeInternal(bytes, typeof(Dictionary<string, object>));
        }

        public virtual byte[] SerializeEvent(object domainEvent, int format)
        {
            return SerializeInternal(domainEvent);
        }

        public virtual byte[] SerializeMetadata(IReadOnlyDictionary<string, object> metadata)
        {
            return SerializeInternal(metadata);
        }

        private static byte[] SerializeInternal(object o)
        {
            using (var ms = new MemoryStream())
            using (var writer = new BsonWriter(ms))
            {
                var serializer = new JsonSerializer();
                serializer.Serialize(writer, o);
                return ms.ToArray();
            }
        }

        private static object DeserializeInternal(byte[] bytes, Type type)
        {
            using (var ms = new MemoryStream(bytes))
            using (var reader = new BsonReader(ms))
            {
                var serializer = new JsonSerializer();
                return serializer.Deserialize(reader, type);
            }
        }
    }
}
