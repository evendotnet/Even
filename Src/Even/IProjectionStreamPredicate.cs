using System;
using System.Diagnostics.Contracts;

namespace Even
{
    public interface IProjectionStreamPredicate
    {
        bool EventMatches(IPersistedEvent persistedEvent);
        object GetDeterministicHashSource();
    }

    public class DomainEventPredicate : IProjectionStreamPredicate
    {
        public DomainEventPredicate(Type domainEventType)
        {
            Contract.Requires(domainEventType != null);
            _type = domainEventType;
        }

        Type _type;

        public bool EventMatches(IPersistedEvent persistedEvent)
        {
            return _type.IsAssignableFrom(persistedEvent.DomainEvent.GetType());
        }

        public object GetDeterministicHashSource()
        {
            return GetType().Name + _type.FullName + _type.Assembly.GetName().FullName;
        }
    }
}
