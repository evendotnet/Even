using NSubstitute;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even.Tests.Mocks
{
    public static class MockPersistedEvent
    {
        public static IPersistedEvent<T> Create<T>(T domainEvent, int globalSequence = 1, Stream stream = null)
        {
            stream = stream ?? "a";

            var e = Substitute.For<IPersistedEvent<T>, IPersistedEvent>();

            e.DomainEvent.Returns(domainEvent);
            e.GlobalSequence.Returns(globalSequence);
            e.Stream.Returns(stream);
            ((IPersistedEvent)e).DomainEvent.Returns(domainEvent);

            return e;
        }
    }
}
