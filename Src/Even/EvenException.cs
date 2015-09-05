using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even
{
    public class EvenException : Exception
    { }

    /// <summary>
    /// This exception is thrown by the store when the expected sequence does not match the store.
    /// </summary>
    public class UnexpectedStreamSequenceException : EvenException
    { }

    public class DeserializationException : EvenException
    { }
}
