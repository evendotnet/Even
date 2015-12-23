using NSubstitute;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Even.Messages;
using Akka.Actor;
using Even.Internals;
using Even.Tests.Mocks;

namespace Even.Tests
{
    public class ProjectionIndexWriterTests : EvenTestKit
    {
        #region Helpers

        IActorRef CreateWriter(IProjectionStoreWriter writer = null, TimeSpan? flushDelay = null)
        {
            writer = writer ?? MockProjectionStore.SuccessfulWriter();
            var delay = flushDelay ?? TimeSpan.FromMilliseconds(10);
            var props = ProjectionIndexWriter.CreateProps(writer, new GlobalOptions { IndexWriterFlushDelay = delay });
            return Sys.ActorOf(props);
        }

        #endregion

        [Fact]
        public async Task Writer_buffers_requests_before_writing()
        {
            var store = MockProjectionStore.SuccessfulWriter();
            store.WriteProjectionIndexAsync(null, 0, null).ReturnsForAnyArgs(Unit.GetCompletedTask());

            var writer = CreateWriter(store);
            writer.Tell(new ProjectionIndexPersistenceRequest("a", 1, 10));
            writer.Tell(new ProjectionIndexPersistenceRequest("a", 2, 20));
            writer.Tell(new ProjectionIndexPersistenceRequest("a", 3, 30));

            await Task.Delay(200);
            var calls = store.ReceivedCalls().ToList();

            var expectedSequences = new long[] { 10, 20, 30 };

            #pragma warning disable 4014
            store.Received().WriteProjectionIndexAsync("a", 0, Arg.Is<IReadOnlyCollection<long>>(l => l.SequenceEqual(expectedSequences)));
            #pragma warning restore 4014
        }

        [Fact]
        public void Writer_replies_inconsistency_message_on_missing_sequence()
        {
            var writer = CreateWriter();
            writer.Tell(new ProjectionIndexPersistenceRequest("a", 1, 10));
            writer.Tell(new ProjectionIndexPersistenceRequest("a", 2, 20));
            writer.Tell(new ProjectionIndexPersistenceRequest("a", 4, 40));

            ExpectMsg<ProjectionIndexInconsistencyDetected>();
        }

        [Fact]
        public void Writer_replies_inconsistency_message_on_unexpected_sequence()
        {
            var store = MockProjectionStore.ThrowsOnWrite(new UnexpectedStreamSequenceException());
            var writer = CreateWriter(store);

            writer.Tell(new ProjectionIndexPersistenceRequest("a", 1, 1));
            ExpectMsg<ProjectionIndexInconsistencyDetected>();
        }

        [Fact]
        public void Writer_replies_inconsistency_message_on_duplicated_request()
        {
            var store = MockProjectionStore.ThrowsOnWrite(new DuplicatedEntryException());
            var writer = CreateWriter(store);

            writer.Tell(new ProjectionIndexPersistenceRequest("a", 1, 1));
            ExpectMsg<ProjectionIndexInconsistencyDetected>();
        }

        [Fact]
        public void Writer_does_not_reply_on_unexpected_exceptions()
        {
            // in case of unexpected exceptions, like connection issues
            // the way to handle is to let the projection detect inconsistencies when the write works
            // and rebuild the index if needed, so we don't expect any error messages

            var store = MockProjectionStore.ThrowsOnWrite(new Exception());
            var writer = CreateWriter(store);

            writer.Tell(new ProjectionIndexPersistenceRequest("a", 1, 1));
            ExpectNoMsg(TimeSpan.FromSeconds(1));
        }
    }
}
