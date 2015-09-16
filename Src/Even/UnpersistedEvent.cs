using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Even
{
    public class UnpersistedEvent
    {
        public UnpersistedEvent(string streamId, object domainEvent)
            : this(streamId, domainEvent, null, null)
        { }

        public UnpersistedEvent(string streamId, object domainEvent, string eventType, Dictionary<string, object> metadata)
        {
            Argument.Requires<ArgumentException>(streamId != null, nameof(streamId));
            Argument.Requires<ArgumentException>(domainEvent != null, nameof(domainEvent));

            this.StreamID = streamId;
            this.DomainEvent = domainEvent;
            this.EventType = eventType ?? GetEventType(domainEvent);

            if (eventType != Constants.AnonymousEventType)
            {
                metadata = metadata ?? new Dictionary<string, object>(1);
                metadata[Constants.ClrTypeMetadataKey] = GetUnversionedQualifiedName(domainEvent.GetType());
            }

            this.Metadata = metadata;
        }

        public Guid EventID { get; } = Guid.NewGuid();
        public DateTime UtcTimestamp { get; } = DateTime.UtcNow;
        public string StreamID { get; }
        public string EventType { get; }
        public object DomainEvent { get; }
        public IReadOnlyDictionary<string, object> Metadata { get; }

        private static string GetEventType(object o)
        {
            var type = o.GetType();

            var esEvent = type.GetCustomAttributes(typeof(ESEventAttribute), false).FirstOrDefault() as ESEventAttribute;

            if (esEvent != null)
                return esEvent.EventType;

            if (IsAnonymousType(type))
                return Constants.AnonymousEventType;

            return type.Name;
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
