using Even.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even.Tests.Mocks
{
    public class TestStore : InMemoryStore
    {
        public TestStore(IEnumerable<UnpersistedEvent> events)
        {
            var rawEvents = UnpersistedRawEvent.FromUnpersistedEvents(events, new DefaultSerializer());
            this.WriteAsync(rawEvents);
        }
    }
}
