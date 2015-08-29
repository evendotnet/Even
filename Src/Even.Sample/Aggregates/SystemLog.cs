using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even.Sample.Aggregates
{
    [ESCategory("systemlog")]
    public class SystemLog : Aggregate
    {
        protected override bool CanAcceptStream(string streamId)
        {
            return streamId == Category;
        }
    }
}
