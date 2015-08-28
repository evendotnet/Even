using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even
{
    public class StreamEventAttribute : Attribute
    {
        public StreamEventAttribute(string name)
        {
            Contract.Requires(String.IsNullOrEmpty(name));
            this.Name = name;
        }

        public string Name { get; }
    }
}
