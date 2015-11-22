using Akka.Actor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Even.Messages;
using System.Diagnostics.Contracts;

namespace Even
{
    public class EvenGateway 
    {
        public EvenGateway(EvenServices services, ActorSystem system, GlobalOptions options)
        {
            Argument.RequiresNotNull(services, nameof(services));
            Argument.RequiresNotNull(system, nameof(system));
            Argument.RequiresNotNull(options, nameof(options));

            this._system = system;
            this.Services = services;
            this._options = options;
        }

        public EvenServices Services { get; }
        ActorSystem _system;
        GlobalOptions _options;

        /// <summary>
        /// Sends a command to an aggregate using the aggregate's category as the stream id.
        /// </summary>
        public Task SendAggregateCommand<T>(object command, TimeSpan? timeout = null)
            where T : Aggregate
        {
            var stream = ESCategoryAttribute.GetCategory(typeof(T));
            return SendAggregateCommand<T>(stream, command, timeout);
        }

        /// <summary>
        /// Sends a command to an aggregate using the aggregate's category and an id to compose the stream id.
        /// The stream will be generated as "category-id".
        /// </summary>
        public Task<CommandResult> SendAggregateCommand<T>(object id, object command, TimeSpan? timeout = null)
            where T : Aggregate
        {
            Contract.Requires(id != null);

            var category = ESCategoryAttribute.GetCategory(typeof(T));
            var stream = id != null ? category + "-" + id.ToString() : category;

            return SendAggregateCommand<T>(stream, command, timeout);
        }

        /// <summary>
        /// Sends a command to an aggregate using the specified stream id.
        /// </summary>
        public Task<CommandResult> SendAggregateCommand<T>(string stream, object command, TimeSpan? timeout = null)
            where T : Aggregate
        {
            Argument.RequiresNotNull(stream, nameof(stream));
            Argument.RequiresNotNull(command, nameof(command));

            var to = timeout ?? _options.DefaultCommandTimeout;

            var aggregateCommand = new AggregateCommand(stream, command, to);
            var envelope = new AggregateCommandEnvelope(typeof(T), aggregateCommand);

            // TODO: add some threshold to Ask higher than the timeout
            return Ask(Services.Aggregates, envelope, to);
        }

        private static async Task<CommandResult> Ask(IActorRef actor, object msg, TimeSpan timeout)
        {
            object response;

            try
            {
                response = (CommandResponse)await actor.Ask(msg, timeout);
            }
            catch (TaskCanceledException)
            {
                throw new CommandException("Command timeout");
            }

            if (response is CommandSucceeded)
                return new CommandResult();

            if (response is CommandRejected)
                return new CommandResult(((CommandRejected)response).Reasons);

            if (response is CommandFailed)
            {
                var cf = (CommandFailed)response;
                throw new CommandException("An error occoured while processing the command: " + cf.Reason, cf.Exception);
            }

            throw new UnexpectedCommandResponseException(response);
        }

        public Task<TResponse> Query<TResponse>(object query, TimeSpan? timeout = null)
        {
            var to = timeout ?? _options.DefaultQueryTimeout;
            return _system.Query<TResponse>(query, timeout ?? _options.DefaultQueryTimeout);
        }
    }
}
