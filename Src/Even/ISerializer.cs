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
    public interface ISerializer
    {
        byte[] SerializeEvent(object domainEvent, int format);
        object DeserializeEvent(byte[] bytes, int format, Type type);

        byte[] SerializeMetadata(IReadOnlyDictionary<string, object> metadata);
        IReadOnlyDictionary<string, object> DeserializeMetadata(byte[] bytes);
    }
}
