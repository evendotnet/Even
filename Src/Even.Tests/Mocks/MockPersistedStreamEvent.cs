using NSubstitute;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even.Tests.Mocks
{
    public static class MockPersistedStreamEvent
    {
        public static IPersistedStreamEvent<T> Create<T>(T domainEvent, int globalSequence = 1, int streamSequence = 1, string streamId = "a")
        {
            var e = Substitute.For<IPersistedStreamEvent<T>, IPersistedStreamEvent>();

            e.DomainEvent.Returns(domainEvent);
            e.GlobalSequence.Returns(globalSequence);
            e.StreamSequence.Returns(streamSequence);
            e.StreamID.Returns(streamId);
            ((IPersistedStreamEvent)e).DomainEvent.Returns(domainEvent);

            return e;
        }
    }
}
