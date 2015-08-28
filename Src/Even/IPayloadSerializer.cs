using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even
{
    interface IPayloadSerializer
    {
        byte[] Serialize(object payload);
        object Deserialize(Type type, byte[] data);
    }
}
