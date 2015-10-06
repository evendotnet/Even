using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even
{
    public interface IProjectionOptions
    {
        TimeSpan ProjectionReplayTimeout { get; }
    }

    public interface IProjectionStreamOptions
    {

    }

    public class GlobalOptions
    {
        /// <summary>
        /// The maximum number of events per read request.
        /// </summary>
        public int EventsPerReadRequest { get; set; } = 10000;

        /// <summary>
        /// The default timeout for reveiving read responses.
        /// </summary>
        public TimeSpan ReadRequestTimeout { get; set; } = TimeSpan.FromSeconds(15);

        /// <summary>
        /// The amount of time the projection will wait before starting to replay;
        /// </summary>
        public TimeSpan ProjectionReplayDelay { get; set; } = TimeSpan.FromSeconds(1);

        public TimeSpan ProjectionReplayRetryInterval { get; set; } = TimeSpan.FromSeconds(1);
        public int MaxProjectionReplayRetries { get; set; } = 10;

        /// <summary>
        /// The default timeout for commands sent through the gateway.
        /// </summary>
        public TimeSpan DefaultCommandTimeout { get; set; } = TimeSpan.FromSeconds(10);

        public TimeSpan AggregateFirstCommandTimeout { get; set; } = TimeSpan.FromSeconds(1);
        public TimeSpan AggregateIdleTimeout { get; set; } = TimeSpan.FromSeconds(60);
        public TimeSpan AggregatePersistenceTimeout { get; set; } = TimeSpan.FromSeconds(30);
        public int MaxAggregateProcessAttempts { get; set; } = 10;
        public TimeSpan AggregateStopTimeout { get; set; } = TimeSpan.FromSeconds(10);
        public TimeSpan DispatcherRecoveryTimeout { get; set; } = TimeSpan.FromSeconds(5);
    }
}
