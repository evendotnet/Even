using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Even
{
    /// <summary>
    /// Represents an unpersisted event.
    /// </summary>
    public class UnpersistedEvent
    {
        private static readonly string AnonymousEventType = "$AnonymousEvent";

        public UnpersistedEvent(object domainEvent)
            : this(domainEvent, GetEventType(domainEvent), null, Guid.NewGuid(), DateTime.UtcNow)
        { }

        public UnpersistedEvent(object domainEvent, string eventType)
            : this(domainEvent, eventType, null, Guid.NewGuid(), DateTime.UtcNow)
        { }

        public UnpersistedEvent(object domainEvent, string eventType, Dictionary<string, object> metaData)
            : this(domainEvent, eventType, metaData, Guid.NewGuid(), DateTime.UtcNow)
        { }

        public UnpersistedEvent(object domainEvent, string eventType, Dictionary<string, object> metaData, Guid eventId, DateTime utcTimestamp)
        {
            Contract.Requires(domainEvent != null);
            Contract.Requires(!String.IsNullOrEmpty(eventType));
            Contract.Requires(eventId != Guid.Empty);
            Contract.Requires(utcTimestamp > DateTime.MinValue);

            this.EventID = eventId;
            this.UtcTimestamp = utcTimestamp;
            this.EventType = eventType;
            this.DomainEvent = domainEvent;

            if (eventType != AnonymousEventType)
            {
                metaData = metaData ?? new Dictionary<string, object>();
                metaData["$CLRType"] = GetUnversionedQualifiedName(domainEvent.GetType());
            }

            this.Metadata = metaData;
        }

        public Guid EventID { get; }
        public DateTime UtcTimestamp { get; }
        public string EventType { get; }
        public object DomainEvent { get; }
        public IReadOnlyDictionary<string, object> Metadata { get; }

        private static string GetEventType(object o)
        {
            if (o != null)
            {
                var type = o.GetType();

                var esEvent = type.GetCustomAttributes(typeof(ESEventAttribute), false).FirstOrDefault() as ESEventAttribute;

                if (esEvent != null)
                    return esEvent.EventType;

                if (IsAnonymousType(type))
                    return AnonymousEventType;

                return type.Name;
            }

            return null;
        }

        private static bool IsAnonymousType(Type type)
        {
            return type.GetCustomAttributes(typeof(CompilerGeneratedAttribute), false).Length > 0 && type.FullName.Contains("AnonymousType");
        }

        private static string GetUnversionedQualifiedName(Type type)
        {
            return type.FullName + ", " + type.Assembly.GetName().Name;
        }
    }
}
