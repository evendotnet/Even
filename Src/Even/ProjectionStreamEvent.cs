using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even
{
    public class ProjectionStreamEvent : IProjectionStreamEvent
    {
        public ProjectionStreamEvent(string queryID, int sequence, int sequenceHash, IStreamEvent @event)
        {
            this.QueryID = queryID;
            this.Sequence = sequence;
            this.SequenceHash = sequenceHash;
            this.Event = @event;
        }

        public string QueryID { get; }
        public int Sequence { get; }
        public int SequenceHash { get; }
        public IStreamEvent Event { get; }
    }
}
