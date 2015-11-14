using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even.Messages
{
    public class ProcessorCommandEnvelope
    {
        public ProcessorCommandEnvelope(Type aggregateType, ProcessorCommand command)
        {
            Argument.RequiresNotNull(aggregateType, nameof(aggregateType));
            Argument.RequiresNotNull(command, nameof(command));

            this.ProcessorType = aggregateType;
            this.Command = command;
        }

        public Type ProcessorType { get; }
        public ProcessorCommand Command { get; }
    }

    public class ProcessorCommand
    {
        public ProcessorCommand(object command, Timeout timeout)
        {
            Argument.RequiresNotNull(command, nameof(command));

            this.CommandID = Guid.NewGuid();
            this.Command = command;
            this.Timeout = timeout;
        }

        public Guid CommandID { get; }
        public object Command { get; }
        public Timeout Timeout { get; }
    }
}