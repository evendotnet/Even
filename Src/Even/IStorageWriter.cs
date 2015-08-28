using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Even
{
    public interface IStorageWriter
    {
        Task WriteEvents(IEnumerable<IRawStorageEvent> events, Action<IRawStorageEvent, long> writtenCallback);
    }
}