using NSubstitute;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even.Tests.Mocks
{
    public static class MockEventStore
    {
        public static IEventStoreWriter SuccessfulWriter()
        {
            var store = Substitute.For<IEventStoreWriter>();

            store.WriteStreamAsync(null, 0, Arg.Do<IReadOnlyCollection<IUnpersistedRawEvent>>(events =>
            {
                int i = 1;

                foreach (var e in events)
                    e.GlobalSequence = i++;

            })).ReturnsForAnyArgs(Task.CompletedTask);


            store.WriteAsync(Arg.Do<IReadOnlyCollection<IUnpersistedRawStreamEvent>>(events =>
            {
                int i = 1;

                foreach (var e in events)
                    e.GlobalSequence = i++;

            })).ReturnsForAnyArgs(Task.CompletedTask);

            return store;
        }

        public static IEventStoreWriter ThrowsOnWrite<T>(int[] throwOnCalls = null)
            where T : Exception, new()
        {
            return ThrowsOnWrite(new T(), throwOnCalls);
        }

        public static IEventStoreWriter ThrowsOnWrite(Exception exception, int[] throwOnCalls = null)
        {
            throwOnCalls = throwOnCalls ?? new[] { 1 };

            var store = Substitute.For<IEventStoreWriter>();

            var writeCount = 0;

            store.WriteAsync(null).ReturnsForAnyArgs(t => {

                if (throwOnCalls.Contains(++writeCount))
                    throw exception;

                return Task.CompletedTask;
            });

            var writeStreamCount = 0;

            store.WriteStreamAsync(null, 0, null).ReturnsForAnyArgs(t => {

                if (throwOnCalls.Contains(++writeStreamCount))
                    throw exception;

                return Task.CompletedTask;
            });

            return store;
        }
    }
}
