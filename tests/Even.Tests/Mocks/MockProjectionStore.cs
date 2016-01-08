using NSubstitute;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Even.Internals;

namespace Even.Tests.Mocks
{
    public static class MockProjectionStore
    {
        public static IProjectionStoreWriter SuccessfulWriter()
        {
            var store = Substitute.For<IProjectionStoreWriter>();

            store.WriteProjectionCheckpointAsync(null, 0).ReturnsForAnyArgs(Unit.GetCompletedTask());
            store.WriteProjectionIndexAsync(null, 0, null).ReturnsForAnyArgs(Unit.GetCompletedTask());
            store.ClearProjectionIndexAsync(null).ReturnsForAnyArgs(Unit.GetCompletedTask());

            return store;
        }

        public static IProjectionStoreWriter ThrowsOnWrite(Exception exception)
        {
            var store = Substitute.For<IProjectionStoreWriter>();

            store.WriteProjectionIndexAsync(null, 0, null).ReturnsForAnyArgs(_ => { throw exception; });
            store.WriteProjectionCheckpointAsync(null, 0).ReturnsForAnyArgs(_ => { throw exception; });

            return store;
        }
    }
}
