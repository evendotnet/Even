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
    /// Thrown by the store when a write fails due to the stream not being in a specific sequence.
    /// </summary>
    public class UnexpectedStreamSequenceException : EvenException
    { }

    /// <summary>
    /// Thrown by the store when a write fails due to a duplicated entry.
    /// </summary>
    public class DuplicatedEntryException : EvenException
    {
        public DuplicatedEntryException()
        { }

        public DuplicatedEntryException(Exception innerException)
            : base("A duplicated entry was detected.", innerException)
        { }
    }

    public class MissingIndexEntryException : EvenException
    { }
}
