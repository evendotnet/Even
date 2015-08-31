using System;

namespace Even
{
    public interface IStreamPredicate
    {
        bool EventMatches(IEvent persistedEvent);
        object GetDeterministicHashSource();
    }

    public class StreamQuery : IStreamPredicate
    {
        public string StreamID { get; set; }

        public bool EventMatches(IEvent @event)
        {
            return String.Equals(@event.StreamID, StreamID, StringComparison.OrdinalIgnoreCase);
        }

        public object GetDeterministicHashSource()
        {
            return StreamID;
        }
    }

    public class TypedEventQuery<T> : IStreamPredicate
    {
        public bool EventMatches(IEvent persistedEvent)
        {
            return persistedEvent.DomainEvent is T;
        }

        public object GetDeterministicHashSource()
        {
            var t = typeof(T);
            return "Type" + t.FullName + t.Assembly.GetName().FullName;
        }
    }

    //public class EventNameQuery : IStreamPredicate
    //{
    //    public string Category { get; set; }
    //    public string EventName { get; set; }

    //    public object GetDeterministicHashSource()
    //    {
    //        return new string[] { Category, EventName };
    //    }
    //}
}
