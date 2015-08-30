using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even
{
    public class QueryHint
    {
        public IReadOnlyCollection<string> EventNames { get; set; }
        public IReadOnlyCollection<string> Categories { get; set; }
    }
}
