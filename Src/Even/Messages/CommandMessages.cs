using Akka.Actor;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even.Messages
{
    public class AggregateCommandEnvelope
    {
        public AggregateCommandEnvelope(Type aggregateType, AggregateCommand command)
        {
            Argument.RequiresNotNull(aggregateType, nameof(aggregateType));
            Argument.RequiresNotNull(command, nameof(command));

            this.AggregateType = aggregateType;
            this.Command = command;
        }

        public Type AggregateType { get; }
        public AggregateCommand Command { get; }
    }

    /// <summary>
    /// Represents a command sent to an aggregate.
    /// </summary>
    public class AggregateCommand
    {
        internal AggregateCommand(AggregateCommand previous)
        {
            this.CommandID = previous.CommandID;
            this.StreamID = previous.StreamID;
            this.Command = previous.Command;
            this.Timeout = previous.Timeout;
        }

        public AggregateCommand(string streamId, object command, TimeSpan timeout)
        {
            Argument.RequiresNotNull(streamId, nameof(streamId));
            Argument.RequiresNotNull(command, nameof(command));

            this.CommandID = Guid.NewGuid();
            this.StreamID = streamId;
            this.Command = command;
            this.Timeout = Timeout.In(timeout);
        }

        public Guid CommandID { get; }
        public string StreamID { get; }
        public object Command { get; }
        public Timeout Timeout { get; }
    }

    public class RetryAggregateCommand : AggregateCommand
    {
        public RetryAggregateCommand(AggregateCommand command, int attemptNo)
            : base(command)
        {
            this.Attempt = attemptNo;
        }

        public int Attempt { get; }
    }

    public class CommandResponse
    {
        public CommandResponse(Guid commandId)
        {
            this.CommandID = commandId;
        }

        public Guid CommandID { get; }
    }

    /// <summary>
    /// Indicates the command was applied successfully.
    /// </summary>
    public class CommandSucceeded : CommandResponse
    {
        public CommandSucceeded(Guid commandId)
            : base(commandId)
        { }
    }

    public class CommandRejected : CommandResponse
    {
        public CommandRejected(Guid commandId, RejectReasons reasons)
            : base(commandId)
        {
            Argument.RequiresNotNull(reasons, nameof(reasons));

            this.Reasons = reasons;
        }

        public RejectReasons Reasons { get; }
    }

    /// <summary>
    /// Indicates the command failed for some reason.
    /// </summary>
    public class CommandFailed : CommandResponse
    {
        public CommandFailed(Guid commandId, Exception ex)
            : base(commandId)
        {
            this.Exception = ex;
            this.Reason = ex.Message;
        }

        public CommandFailed(Guid commandId, string reason)
            : base(commandId)
        {
            this.Reason = reason;
        }

        public string Reason { get; }
        public Exception Exception { get;  }
    }

    public class CommandTimeout : CommandResponse
    {
        public CommandTimeout(Guid commandId)
            : base(commandId)
        { }
    }

    public class CommandRefused : CommandResponse
    {
        public CommandRefused(Guid commandId, string reason)
            : base(commandId)
        {
            this.Reason = reason;
        }

        public AggregateCommand CommandRequest { get; set; }
        public string Reason { get; set; }
    }
}
