using NSubstitute;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even.Tests
{
    public static class MockStore
    {
        public static IEventStoreWriter Default()
        {
            var store = Substitute.For<IEventStoreWriter>();

            store.WriteStreamAsync(null, 0, Arg.Do<IReadOnlyCollection<UnpersistedRawEvent>>(events =>
            {
                int i = 1;

                foreach (var e in events)
                    e.SetGlobalSequence(i++);

            })).ReturnsForAnyArgs(Task.CompletedTask);


            store.WriteAsync(Arg.Do<IReadOnlyCollection<UnpersistedRawStreamEvent>>(events =>
            {
                int i = 1;

                foreach (var e in events)
                    e.SetGlobalSequence(i++);

            })).ReturnsForAnyArgs(Task.CompletedTask);

            return store;
        }

        public static IEventStoreWriter ThrowsOnWrite<T>()
            where T : Exception, new()
        {
            return ThrowsOnWrite(new T());
        }

        public static IEventStoreWriter ThrowsOnWrite(Exception exception)
        {
            var store = Substitute.For<IEventStoreWriter>();

            store.WriteAsync(null).ReturnsForAnyArgs(t => { throw exception; });
            store.WriteStreamAsync(null, 0, null).ReturnsForAnyArgs(t => { throw exception; });

            return store;
        }
    }

}
