using System;
using System.Diagnostics.Contracts;

namespace Even
{
    public class UnpersistedRawEvent
    {
        public UnpersistedRawEvent(Guid eventId, DateTime utcTimestamp, string eventType, byte[] metadata, byte[] payload, int payloadFormat)
        {
            EventID = eventId;
            UtcTimestamp = utcTimestamp;
            EventType = eventType;
            Metadata = metadata;
            Payload = payload;
            PayloadFormat = payloadFormat;
        }

        public Guid EventID { get; }
        public DateTime UtcTimestamp { get; }
        public string EventType { get; }
        public byte[] Metadata { get; }
        public byte[] Payload { get; }
        public int PayloadFormat { get; }

        public long GlobalSequence { get; private set; }
        public int StreamSequence { get; private set; }

        public bool SequencesWereSet { get; private set; }

        public void SetSequences(long globalSequence, int streamSequence)
        {
            Contract.Requires(globalSequence > 0);
            Contract.Requires(streamSequence > 0);

            if (SequencesWereSet)
                throw new InvalidOperationException("Sequences should be set only once.");

            GlobalSequence = globalSequence;
            StreamSequence = streamSequence;

            SequencesWereSet = true;
        }
    }
}
