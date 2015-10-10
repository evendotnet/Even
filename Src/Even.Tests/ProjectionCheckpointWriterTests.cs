using Akka.Actor;
using Even.Messages;
using NSubstitute;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Even.Tests
{
    public class ProjectionCheckpointWriterTests : EvenTestKit
    {
        [Fact]
        public async Task Writer_buffers_and_writes_only_highest_value()
        {
            var store = Substitute.For<IProjectionStoreWriter>();
            var props = ProjectionCheckpointWriter.CreateProps(store, new GlobalOptions { CheckpointWriterFlushDelay = TimeSpan.FromMilliseconds(10) });
            var writer = Sys.ActorOf(props);

            writer.Tell(new ProjectionCheckpointPersistenceRequest("a", 10));
            writer.Tell(new ProjectionCheckpointPersistenceRequest("a", 20));
            writer.Tell(new ProjectionCheckpointPersistenceRequest("a", 30));
            writer.Tell(new ProjectionCheckpointPersistenceRequest("a", 40));
            writer.Tell(new ProjectionCheckpointPersistenceRequest("a", 50));

            await Task.Delay(200);

            #pragma warning disable 4014
            store.Received().WriteProjectionCheckpointAsync("a", 50);
            #pragma warning restore 4014
        }

        [Fact]
        public void Writer_does_not_reply_failure_messages_on_store_exception()
        {
            var store = Substitute.For<IProjectionStoreWriter>();
            store.WriteProjectionCheckpointAsync(null, 0).ReturnsForAnyArgs(_ => { throw new Exception(); });

            var props = ProjectionCheckpointWriter.CreateProps(store, new GlobalOptions { CheckpointWriterFlushDelay = TimeSpan.Zero });
            var writer = Sys.ActorOf(props);

            writer.Tell(new ProjectionCheckpointPersistenceRequest("a", 1));

            ExpectNoMsg(TimeSpan.FromMilliseconds(200));
        }
    }
}
