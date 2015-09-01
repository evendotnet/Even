using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even
{
    public class EvenException : Exception
    { }

    public class StorageSequenceException : EvenException
    { }

    public class DeserializationException : EvenException
    { }
}
