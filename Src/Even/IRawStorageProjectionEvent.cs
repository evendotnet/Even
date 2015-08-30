using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even
{
    public interface IRawStorageProjectionEvent : IRawStorageEvent
    {
        string ProjectionID { get; }
        int ProjectionSequence { get; }
    }

    public class RawStorageProjectionEvent : IRawStorageProjectionEvent
    {
        public RawStorageProjectionEvent(string projectionId, int projectionSequence, IRawStorageEvent e)
        {
            ProjectionID = projectionId;
            ProjectionSequence = ProjectionSequence;
            _e = e;
        }

        private IRawStorageEvent _e;

        public string ProjectionID { get; }
        public int ProjectionSequence { get; }

        public long Checkpoint => _e.Checkpoint;
        public Guid EventID => _e.EventID;
        public string EventName => _e.EventName;
        public byte[] Headers => _e.Headers;
        public byte[] Payload => _e.Payload;
        public string StreamID => _e.StreamID;
        public int StreamSequence => _e.StreamSequence;
    }
}
