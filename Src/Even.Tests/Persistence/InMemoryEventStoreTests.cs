using Even.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Even.Tests.Persistence
{
    public class InMemoryEventStoreTests : EventStoreTests
    {
        protected override IEventStore CreateStore()
        {
            return new InMemoryStore();
        }

        protected override void ResetStore()
        { }
    }
}
