using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even
{
    public interface IPersistedStreamEvent : IPersistedEvent
    {
        int StreamSequence { get; }
    }

    public interface IPersistedStreamEvent<T> : IPersistedStreamEvent, IPersistedEvent<T>
    { }
}
