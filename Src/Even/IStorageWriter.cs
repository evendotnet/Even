using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Even
{
    public interface IStorageWriter
    {
        Task WriteEvents(IEnumerable<IRawStorageEvent> events, Action<IRawStorageEvent, long> writtenCallback);

        Task WriteSnapshot(string streamId, IRawAggregateSnapshot snapshot);
        Task WriteProjectionStreamIndex(IEnumerable<IProjectionStreamIndex> entries);
        Task ClearProjectionIndex(string projectionId);
    }
}