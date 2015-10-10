using Even.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even
{
    public class CommandResult
    {
        public CommandResult()
        {
            Accepted = true;
        }

        public CommandResult(RejectReasons reasons)
        {
            Accepted = false;
            RejectReasons = reasons;
        }

        public bool Accepted { get; private set; }
        public RejectReasons RejectReasons { get; private set; }
    }
}
