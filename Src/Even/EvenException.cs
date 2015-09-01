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
    public class UnexpectedSequenceException : EvenException
    { }

    public class DeserializationException : EvenException
    { }

    /// <summary>
    /// This exception is thrown when the store writer tries to persist an invalid sequence during strict write.
    /// </summary>
    public class StrictEventWriteException : EvenException
    { }
}
