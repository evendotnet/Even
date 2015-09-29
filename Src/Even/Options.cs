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
    }
}
