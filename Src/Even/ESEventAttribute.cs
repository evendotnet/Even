using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even
{
    public class ESEventAttribute : Attribute
    {
        public ESEventAttribute()
        { }

        public ESEventAttribute(string eventType)
        {
            Contract.Requires(!String.IsNullOrEmpty(eventType));
            this.EventType = eventType;
        }

        public string EventType { get; }

        public static string GetEventType(Type type)
        {
            var a = Attribute.GetCustomAttribute(type, typeof(ESEventAttribute)) as ESEventAttribute;
            return a.EventType ?? type.Name;
        }
    }
}
