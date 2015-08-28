using Akka.Actor;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even.Messages
{
    // base messages
    public abstract class AggregateCommandMessage
    {
        public Guid CommandID { get; set; }
    }

    /// <summary>
    /// Represents a request to send a command to a specific aggregate
    /// </summary>
    public class AggregateCommandRequest : AggregateCommandMessage
    {
        public string StreamID { get; set; }
        public object Command { get; set; }
    }

    public class AggregateCommandResponse : AggregateCommandMessage
    { }

    public class TypedAggregateCommandRequest : AggregateCommandRequest
    {
        public Type AggregateType { get; set; }
    }

    /// <summary>
    /// Indicates the command was applied successfully.
    /// </summary>
    public class AggregateCommandSuccessful : AggregateCommandResponse
    { }

    /// <summary>
    /// Indicates the command failed for some reason.
    /// </summary>
    public class AggregateCommandFailed : AggregateCommandResponse
    {
        public Exception Exception { get; set; }
    }

    /// <summary>
    /// Indicates the command timed out.
    /// </summary>
    public class AggregateCommandTimedout : AggregateCommandResponse
    { }

    public class AggregateCommandUnknown : AggregateCommandResponse
    {
        public AggregateCommandRequest CommandRequest { get; set; }
    }
}
