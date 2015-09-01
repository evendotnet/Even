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
        public ESEventAttribute(string name)
        {
            Contract.Requires(String.IsNullOrEmpty(name));
            this.Name = name;
        }

        public string Name { get; }

        public static string GetEventName(Type type)
        {
            var a = Attribute.GetCustomAttribute(type, typeof(ESEventAttribute)) as ESEventAttribute;
            return a?.Name ?? type.Name;
        }
    }
}
