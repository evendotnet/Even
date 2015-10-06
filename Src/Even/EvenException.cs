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

    public class EventOutOfOrderException : EvenException
    {
        public EventOutOfOrderException(long expectedSequence, long receivedSequence, string message = null)
            : base(message, null)
        {
            this.ExpectedSequence = expectedSequence;
            this.ReceivedSequence = receivedSequence;
        }

        public long ExpectedSequence { get; }
        public long ReceivedSequence { get; }
    }

    public class RebuildRequestException : Exception
    { }

    public class UnexpectedCommandResponseException : Exception
    {
        public UnexpectedCommandResponseException(object response)
            : base("An unexpected command response was received.")
        {
            this.Response = response;
        }

        public object Response { get; }
    }

    public class RejectCommandException
    {
        public RejectCommandException(object reason)
        {
            this.Reason = reason;
        }

        public object Reason { get; }
    }
}
