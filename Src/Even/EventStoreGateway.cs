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
    public class EventStoreGateway 
    {
        public IActorRef EventStore { get; set; }
        public IActorRef CommandProcessors { get; set; }
        public IActorRef EventProcessors { get; set; }

        /// <summary>
        /// Sends a command to an aggregate using the aggregate's category and the aggregate id to compose the stream id.
        /// </summary>
        public Task SendCommandAsync<T>(object aggregateId, object command, TimeSpan? timeout = null)
            where T : Aggregate
        {
            Contract.Requires(aggregateId != null);

            var category = ESCategoryAttribute.GetCategory(typeof(T));
            var streamId = aggregateId != null ? category + "-" + aggregateId.ToString() : category;

            return SendCommandAsync<T>(streamId, command, timeout);
        }

        /// <summary>
        /// Sends a command to an aggregate using the aggregate's category as the stream id.
        /// </summary>
        public Task SendCommandAsync<T>(object command, TimeSpan? timeout = null)
            where T : Aggregate
        {
            var streamId = ESCategoryAttribute.GetCategory(typeof(T));
            return SendCommandAsync<T>(streamId, command, timeout);
        }

        /// <summary>
        /// Sends a command to an aggregate using the specified stream id.
        /// </summary>
        public async Task SendCommandAsync<T>(string streamId, object command, TimeSpan? timeout)
            where T : Aggregate
        {
            var request = new TypedCommandRequest
            {
                CommandID = Guid.NewGuid(),
                AggregateType = typeof(T),
                StreamID = streamId,
                Command = command
            };

            // the command gateway translates error messages into exceptions, so the user
            // don't have to deal with messaging to talk interoperate with the event store

            CommandResponse response;

            try
            {
                response = await CommandProcessors.Ask(request, timeout) as CommandResponse;
            }
            catch (TaskCanceledException)
            {
                throw new CommandTimeoutException();
            }

            if (response is CommandSucceeded)
                return;

            if (response is CommandRefused)
                throw new CommandRefusedException();

            if (response is CommandFailed)
                throw new CommandFailedException();

            throw new Exception("Should not get here...");
        }
    }

    public abstract class EvenCommandException : Exception
    { }

    public class CommandFailedException : EvenCommandException
    { }

    public class CommandRefusedException : EvenCommandException
    { }

    public class CommandTimeoutException : EvenCommandException
    { }
}
