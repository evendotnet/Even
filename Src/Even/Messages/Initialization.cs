using Akka.Actor;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even.Messages
{
    public class InitializationResult
    {
        private InitializationResult()
        { }

        public bool Initialized { get; private set; }
        public Exception Exception { get; private set; }

        public static InitializationResult Successful()
        {
            return new InitializationResult { Initialized = true };
        }

        public static InitializationResult Failed(Exception ex)
        {
            return new InitializationResult { Initialized = false, Exception = ex };
        }
    }

    public class InitializeCommandProcessor
    {
        public IActorRef CommandProcessorSupervisor { get; set; }
        public IActorRef Writer { get; set; }
    }

    public class InitializeAggregate
    {
        public InitializeAggregate(IActorRef reader, IActorRef writer, GlobalOptions options)
        {
            Argument.RequiresNotNull(reader, nameof(reader));
            Argument.RequiresNotNull(writer, nameof(writer));
            Argument.RequiresNotNull(options, nameof(options));

            this.Reader = reader;
            this.Writer = writer;
            this.Options = options;
        }

        public IActorRef Reader { get; }
        public IActorRef Writer { get; }
        public GlobalOptions Options { get; }
    }

    public class WillStop
    {
        private WillStop() { }
        public static readonly WillStop Instance = new WillStop();
    }

    public class StopNoticeAcknowledged
    {
        private StopNoticeAcknowledged() { }
        public static readonly StopNoticeAcknowledged Instance = new StopNoticeAcknowledged();
    }

    public class InitializeProjection
    {
        public InitializeProjection(IActorRef projectionStreamSupervisor, GlobalOptions options)
        {
            Argument.RequiresNotNull(projectionStreamSupervisor, nameof(projectionStreamSupervisor));
            Argument.RequiresNotNull(options, nameof(options));

            this.ProjectionStreamSupervisor = projectionStreamSupervisor;
            this.Options = options;
        }

        public IActorRef ProjectionStreamSupervisor { get; }
        public GlobalOptions Options { get; }
    }
}
