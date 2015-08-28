using Akka.Actor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Even.Messages;

namespace Even
{
    public class EventStoreGateway 
    {
        public IActorRef EventStore { get; set; }
        public ICanTell Aggregates { get; set; }
        public ICanTell Projections { get; set; }

        public Task<AggregateCommandResponse> SendCommandAsync<T>(string streamId, object command)
            where T : Aggregate
        {
            return this.SendCommandAsync<T>(streamId, command, null);
        }

        public async Task<AggregateCommandResponse> SendCommandAsync<T>(string streamId, object command, TimeSpan? timeout)
            where T : Aggregate
        {
            var request = new TypedAggregateCommandRequest
            {
                CommandID = Guid.NewGuid(),
                AggregateType = typeof(T),
                StreamID = streamId,
                Command = command
            };

            try
            {
                var response = await Aggregates.Ask(request, timeout) as AggregateCommandResponse;

                if (response != null)
                    return response;
            }
            catch (TaskCanceledException ex)
            {
                return new AggregateCommandTimedout { CommandID = request.CommandID };
            }

            throw new Exception("Should not get here...");
        }
    }
}
