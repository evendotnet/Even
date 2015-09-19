using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even
{
    public class EvenException : Exception
    {
        public EvenException()
        { }

        public EvenException(string message, Exception innerException)
            : base(message, innerException)
        { }
    }

    /// <summary>
    /// This exception is thrown by the store when the expected sequence does not match the store.
    /// </summary>
    public class UnexpectedStreamSequenceException : EvenException
    { }

    public class DuplicatedEntryException : EvenException
    {
        public DuplicatedEntryException()
        { }

        public DuplicatedEntryException(Exception innerException)
            : base("A duplicated entry was detected.", innerException)
        { }
    }

    public class DuplicatedSequenceException : EvenException
    { }

    public class DeserializationException : EvenException
    { }
}
