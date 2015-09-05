using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even
{
    public interface IProjectionRawEvent : IPersistedRawEvent
    {
        string ProjectionStreamID { get; }
        int ProjectionStreamSequence { get; }
    }

    public class ProjectionRawEvent : PersistedRawEvent, IProjectionRawEvent
    {
        public string ProjectionStreamID { get; set; }
        public int ProjectionStreamSequence { get; set; }
    }
}
