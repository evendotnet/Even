using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even
{
    public class EventFactory
    {
        #region IEvent

        public static IEvent CreateEvent(object domainEvent)
        {
            return CreateEvent(domainEvent, Guid.NewGuid(), DateTime.UtcNow);
        }

        public static IEvent CreateEvent(object domainEvent, Guid eventId, DateTime utcTimeStamp)
        {
            return new InternalEvent
            {
                EventID = eventId,
                DomainEvent = domainEvent,
                UtcTimeStamp = utcTimeStamp
            };
        }

        class InternalEvent : IEvent
        {
            public object DomainEvent { get; set; }
            public Guid EventID { get; set; }
            public DateTime UtcTimeStamp { get; set; }
        }

        #endregion

        #region IStreamEvent

        public static IStreamEvent CreateStreamEvent(string streamId, object domainEvent)
        {
            Contract.Requires(!String.IsNullOrWhiteSpace(streamId));
            Contract.Requires(domainEvent != null);

            return new StreamEvent
            {
                EventID = Guid.NewGuid(),
                StreamID = streamId,
                DomainEvent = domainEvent,
                UtcTimeStamp = DateTime.UtcNow
            };
        }

        public static IStreamEvent CreateStreamEvent(string streamId, IEvent @event)
        {
            return new StreamEvent
            {
                EventID = @event.EventID,
                DomainEvent = @event.DomainEvent,
                StreamID = streamId,
                UtcTimeStamp = @event.UtcTimeStamp
            };
        }

        class StreamEvent : IStreamEvent
        {
            public object DomainEvent { get; set; }
            public Guid EventID { get; set; }
            public string StreamID { get; set; }
            public DateTime UtcTimeStamp { get; set; }
        }

        #endregion

        #region IPersistedEvent

        public static IPersistedEvent CreatePersistedEvent(IRawPersistedEvent e, object domainEvent)
        {
            var e2 = CreateEvent(domainEvent, e.EventID, e.UtcTimeStamp);
            return CreatePersistedEvent(e2, e.StreamID, e.Checkpoint, e.StreamSequence);
        }

        public static IPersistedEvent CreatePersistedEvent(IEvent @event, string streamId, long checkpoint, int streamSequence)
        {
            return CreatePersistedEvent(CreateStreamEvent(streamId, @event), checkpoint, streamSequence);
        }

        public static IPersistedEvent CreatePersistedEvent(IStreamEvent streamEvent, long checkpoint, int streamSequence)
        {
            return new PersistedEvent
            {
                Checkpoint = checkpoint,
                StreamSequence = streamSequence,
                StreamEvent = streamEvent
            };
        }

        class PersistedEvent : IPersistedEvent
        {
            public IStreamEvent StreamEvent { get; set; }

            public long Checkpoint { get; set; }
            public int StreamSequence { get; set; }
            public object DomainEvent => StreamEvent.DomainEvent;
            public Guid EventID => StreamEvent.EventID;
            public string StreamID => StreamEvent.StreamID;
            public DateTime UtcTimeStamp => StreamEvent.UtcTimeStamp;
        }

        #endregion

        #region IRawStreamEvent

        public static IRawStreamEvent CreateRawStreamEvent(IRawEvent e, string streamId)
        {
            return new RawStreamEvent2
            {
                StreamID = streamId,
                RawEvent = e
            };
        }

        public static IRawStreamEvent CreateRawStreamEvent(IEvent e, string streamId, string eventName, byte[] headers, byte[] payload)
        {
            var e2 = CreateStreamEvent(streamId, e);
            return CreateRawStreamEvent(e2, eventName, headers, payload);
        }

        public static IRawStreamEvent CreateRawStreamEvent(IStreamEvent e, string eventName, byte[] headers, byte[] payload)
        {
            return new RawStreamEvent
            {
                EventName = eventName,
                Headers = headers,
                Payload = payload,
                StreamEvent = e
            };
        }

        class RawStreamEvent : IRawStreamEvent
        {
            public string EventName { get; set; }
            public byte[] Headers { get; set; }
            public byte[] Payload { get; set; }

            public IStreamEvent StreamEvent { get; set; }

            public Guid EventID => StreamEvent.EventID;
            public string StreamID => StreamEvent.StreamID;
            public DateTime UtcTimeStamp => StreamEvent.UtcTimeStamp;
        }

        class RawStreamEvent2 : IRawStreamEvent
        {
            public string StreamID { get; set; }
            public IRawEvent RawEvent { get; set; }

            public Guid EventID => RawEvent.EventID;
            public string EventName => RawEvent.EventName;
            public DateTime UtcTimeStamp => RawEvent.UtcTimeStamp;
            public byte[] Headers => RawEvent.Headers;
            public byte[] Payload => RawEvent.Payload;
        }

        #endregion

        #region IRawPersistedEvent 

        public static IRawPersistedEvent CreateRawPersistedEvent(long checkpoint, Guid eventId, string streamId, int streamSequence, string eventName, DateTime utcTimeStamp, byte[] headers, byte[] payload)
        {
            return new FullRawPersistedEvent
            {
                Checkpoint = checkpoint,
                EventID = eventId,
                StreamID = streamId,
                StreamSequence = streamSequence,
                EventName = eventName,
                UtcTimeStamp = utcTimeStamp,
                Headers = headers,
                Payload = payload
            };
        }

        public static IRawPersistedEvent CreateRawPersistedEvent(IRawStreamEvent e, long checkpoint, int streamSequence)
        {
            return new RawPersistedEvent
            {
                Checkpoint = checkpoint,
                StreamSequence = streamSequence,
                RawStreamEvent = e
            };
        }

        class RawPersistedEvent : IRawPersistedEvent
        {
            public long Checkpoint { get; set; }
            public int StreamSequence { get; set; }

            public IRawStreamEvent RawStreamEvent;

            public Guid EventID => RawStreamEvent.EventID;
            public string EventName => RawStreamEvent.EventName;
            public byte[] Headers => RawStreamEvent.Headers;
            public byte[] Payload => RawStreamEvent.Payload;
            public string StreamID => RawStreamEvent.StreamID;
            public DateTime UtcTimeStamp => RawStreamEvent.UtcTimeStamp;
        }

        class FullRawPersistedEvent : IRawPersistedEvent
        {
            public long Checkpoint { get; set; }
            public Guid EventID { get; set; }
            public string EventName { get; set; }
            public byte[] Headers { get; set; }
            public byte[] Payload { get; set; }
            public string StreamID { get; set; }
            public int StreamSequence { get; set; }
            public DateTime UtcTimeStamp { get; set; }
        }

        #endregion

        #region IProjectionEvent 

        public static IProjectionEvent CreateProjectionEvent(string projectionStreamID, int projectionSequence, IPersistedEvent @event)
        {
            return new ProjectionEvent
            {
                ProjectionStreamID = projectionStreamID,
                ProjectionSequence = projectionSequence,
                PersistedEvent = @event
            };
        }

        class ProjectionEvent : IProjectionEvent
        {
            public int ProjectionSequence { get; set; }
            public string ProjectionStreamID { get; set; }
            public IPersistedEvent PersistedEvent { get; set; }

            public long Checkpoint => PersistedEvent.Checkpoint;
            public object DomainEvent => PersistedEvent.DomainEvent;
            public Guid EventID => PersistedEvent.EventID;
            public string StreamID => PersistedEvent.StreamID;
            public int StreamSequence => PersistedEvent.StreamSequence;
            public DateTime UtcTimeStamp => PersistedEvent.UtcTimeStamp;
        }

        #endregion

        #region IWrittenEventSequence

        public static IWrittenEventSequence CreateWrittenEventSequence(Guid eventId, long checkpoint, int streamSequence)
        {
            return new WrittenEventSequence
            {
                EventID = eventId,
                Checkpoint = checkpoint,
                StreamSequence = streamSequence
            };
        }

        class WrittenEventSequence : IWrittenEventSequence
        {
            public long Checkpoint { get; set; }
            public Guid EventID { get; set; }
            public int StreamSequence { get; set; }
        }

        #endregion
    }
}
