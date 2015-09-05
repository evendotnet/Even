using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even
{
    public class EventRegistry
    {
        public Dictionary<string, Type> _mapping = new Dictionary<string, Type>(StringComparer.InvariantCultureIgnoreCase);

        public void Register(string eventType, Type clrType)
        {
            Contract.Requires(!String.IsNullOrWhiteSpace(eventType));
            Contract.Requires(clrType != null);

            lock (_mapping)
            {
                if (_mapping.ContainsKey(eventType))
                {
                    var existingType = _mapping[eventType];
                    throw new Exception($"Cannot register '{clrType.AssemblyQualifiedName}' to event type '{eventType}', it's already registered to '{existingType.AssemblyQualifiedName}'");
                }

                _mapping.Add(eventType, clrType);
            }
        }

        public Type GetClrType(string eventType)
        {
            if (eventType == null)
                return null;

            Type t;

            lock (_mapping)
            {
                if (_mapping.TryGetValue(eventType, out t))
                    return t;
            }

            return null;
        }
    }
}
