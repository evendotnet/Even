﻿using Even.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Even.Tests.Persistence
{
    public abstract class EventStoreTests
    {
        public EventStoreTests()
        {
            Store = CreateStore();
            Store.InitializeAsync().Wait();
            ResetStore();
        }

        protected IEventStore Store { get; }
        public object ConcurentBag { get; private set; }

        /// <summary>
        /// Creates the store object.
        /// </summary>
        protected abstract IEventStore CreateStore();

        /// <summary>
        /// Resets the store to an initial state.
        /// </summary>
        protected abstract void ResetStore();

        #region Helpers

        static IUnpersistedRawStreamEvent CreateEvent(string streamName, string eventType)
        {
            return new UnpersistedRawEvent(Guid.NewGuid(), streamName, eventType, DateTime.UtcNow, null, new byte[0], 0);
        }

        static IReadOnlyCollection<IUnpersistedRawStreamEvent> GenerateEvents(int count, string streamName = null)
        {
            var list = new List<IUnpersistedRawStreamEvent>(count);

            for (var i = 0; i < count; i++)
                list.Add(CreateEvent(streamName ?? "SomeStream", "SomeEvent"));

            return list;
        }

        Task WriteTestEvents(int count, string streamName = null)
        {
            return Store.WriteAsync(GenerateEvents(count, streamName));
        }

        async Task WriteAndProjectTestEvents(int count, string streamName, long[] sequencesToIndex)
        {
            await WriteTestEvents(count);
            await Store.WriteProjectionIndexAsync(streamName, 0, sequencesToIndex);
        }

        #endregion

        #region Event Reads

        [Fact]
        public void ReadAsync_Empty_Store_Should_Read_No_Events()
        {
            Store.ReadAsync(0, EventCount.Unlimited, e => { throw new Exception(); }, CancellationToken.None);
        }

        [Fact]
        public void ReadStreamAsync_Empty_Store_Should_Read_No_Events()
        {
            Store.ReadStreamAsync("somestream", 0, EventCount.Unlimited, e => { throw new Exception(); }, CancellationToken.None);
        }

        [Fact]
        public async Task ReadAsync_Unlimited_Count_Should_Read_All_Events()
        {
            await WriteTestEvents(3);

            var count = 0;

            await Store.ReadAsync(0, EventCount.Unlimited, e => count++, CancellationToken.None);

            Assert.Equal(3, count);
        }

        [Fact]
        public async Task ReadStreamAsync_Unlimited_Count_Should_Read_All_Stream_Events()
        {
            await WriteTestEvents(3, "a");

            var count = 0;

            await Store.ReadStreamAsync("a", 0, EventCount.Unlimited, e => count++, CancellationToken.None);

            Assert.Equal(3, count);
        }

        [Theory]
        [InlineData(0, 3, new long[] { 1, 2, 3 })]
        [InlineData(1, 3, new long[] { 1, 2, 3 })]
        [InlineData(2, 3, new long[] { 2, 3, 4 })]
        [InlineData(3, 3, new long[] { 3, 4, 5 })]
        [InlineData(4, 3, new long[] { 4, 5 })]
        [InlineData(5, 3, new long[] { 5 })]
        [InlineData(6, 3, new long[0])]
        public async Task ReadAsync_Considers_Start_And_Count_Correctly(long start, int count, long[] expectedSequences)
        {
            await WriteTestEvents(5);

            var actualSequences = new List<long>();
            await Store.ReadAsync(start, count, e => actualSequences.Add(e.GlobalSequence), CancellationToken.None);

            Assert.Equal(expectedSequences, actualSequences);
        }

        [Theory]
        [InlineData(0, 3, new long[] { 6, 7, 8 })]
        [InlineData(1, 3, new long[] { 6, 7, 8 })]
        [InlineData(2, 3, new long[] { 7, 8, 9 })]
        [InlineData(3, 3, new long[] { 8, 9, 10 })]
        [InlineData(4, 3, new long[] { 9, 10 })]
        [InlineData(5, 3, new long[] { 10 })]
        [InlineData(6, 3, new long[0])]
        public async Task ReadStreamAsync_Considers_Start_And_Count_Correctly(int start, int count, long[] expectedSequences)
        {
            await WriteTestEvents(5, "a");
            await WriteTestEvents(5, "b");

            var actualSequences = new List<long>();
            await Store.ReadStreamAsync("b", start, count, e => actualSequences.Add(e.GlobalSequence), CancellationToken.None);

            Assert.Equal(expectedSequences, actualSequences);
        }

        [Fact]
        public async Task ReadAsync_ZeroCount_Should_Read_No_Events()
        {
            await WriteTestEvents(1);

            await Store.ReadAsync(0, 0, e => { throw new Exception(); }, CancellationToken.None);
        }

        [Fact]
        public async Task ReadStreamAsync_ZeroCount_Should_Read_No_Events()
        {
            await WriteTestEvents(1, "a");

            await Store.ReadStreamAsync("a", 0, 0, e => { throw new Exception(); }, CancellationToken.None);
        }

        [Fact]
        public async Task ReadAsync_Stops_Reading_After_Cancelled()
        {
            await WriteTestEvents(2);
            var cts = new CancellationTokenSource();

            var count = 0;
            await Store.ReadAsync(0, EventCount.Unlimited, e =>
            {
                count++;
                cts.Cancel();

            }, cts.Token);

            Assert.Equal(1, count);
        }

        [Fact]
        public async Task ReadStreamAsync_Stops_Reading_After_Cancelled()
        {
            await WriteTestEvents(2, "foo");
            var cts = new CancellationTokenSource();

            var count = 0;

            await Store.ReadStreamAsync("foo", 0, EventCount.Unlimited, e =>
            {
                count++;
                cts.Cancel();

            }, cts.Token);

            Assert.Equal(1, count);
        }

        #endregion

        #region Event Writes

        [Fact]
        public async Task WriteAsync_Writes_Different_Streams()
        {
            var events = new List<IUnpersistedRawStreamEvent>();

            events.AddRange(GenerateEvents(1, "a"));
            events.AddRange(GenerateEvents(2, "b"));
            events.AddRange(GenerateEvents(3, "c"));

            await Store.WriteAsync(events);

            int aCount = 0, bCount = 0, cCount = 0;

            await Task.WhenAll(
                Store.ReadStreamAsync("a", 0, EventCount.Unlimited, e => aCount++, CancellationToken.None),
                Store.ReadStreamAsync("b", 0, EventCount.Unlimited, e => bCount++, CancellationToken.None),
                Store.ReadStreamAsync("c", 0, EventCount.Unlimited, e => cCount++, CancellationToken.None)
            );

            Assert.Equal(1, aCount);
            Assert.Equal(2, bCount);
            Assert.Equal(3, cCount);
        }

        [Fact]
        public async Task WriteStreamAsync_Throws_On_ExpetedSequence_None()
        {
            await WriteTestEvents(1, "a");

            var events = GenerateEvents(1, "a");

            await Assert.ThrowsAsync<UnexpectedStreamSequenceException>(() =>
                Store.WriteStreamAsync("a", ExpectedSequence.None, events)
            );
        }

        [Fact]
        public async Task WriteStreamAsync_Throws_On_WrongSequence()
        {
            await WriteTestEvents(3, "a");

            var events = GenerateEvents(1, "a");

            await Assert.ThrowsAsync<UnexpectedStreamSequenceException>(() =>
                Store.WriteStreamAsync("a", 2, events)
            );
        }

        [Fact]
        public async Task WriteStreamAsync_Writes_New_Stream_With_Any_Expected_Sequence()
        {
            var events = GenerateEvents(1, "a");
            await Store.WriteStreamAsync("a", ExpectedSequence.Any, events);
        }

        [Fact]
        public async Task WriteStreamAsync_Appends_To_Stream_With_Any_Expected_Sequence()
        {
            await WriteTestEvents(5, "a");
            var events = GenerateEvents(1, "a");
            await Store.WriteStreamAsync("a", ExpectedSequence.Any, events);
        }

        [Fact]
        public async Task WriteAsync_Throws_DuplicatedEntryException()
        {
            var e = CreateEvent("test", "SomeEvent");

            await Store.WriteAsync(new[] { e });

            await Assert.ThrowsAsync<DuplicatedEntryException>(() =>
                Store.WriteAsync(new[] { e })
            );
        }

        [Fact]
        public async Task WriteAsync_can_write_concurrently()
        {
            var taskCount = 20;
            var eventCount = 5;

            var tasks = new List<Task<long[]>>();
            var events = Enumerable.Range(0, taskCount).Select(_ => GenerateEvents(eventCount)).ToList();

            // starts 10 tasks to write 10 events each
            for (var i = 0; i < taskCount; i++)
            {
                var tmp = events[i];

                var t = Task.Run(async () => {
                    await Store.WriteAsync(tmp);
                    return tmp.Select(e => e.GlobalSequence).ToArray();
                });

                tasks.Add(t);
            }

            await Task.WhenAll(tasks);

            var expected = Enumerable.Range(1, taskCount * eventCount).Select(i => (long) i).ToArray();
            var actual = tasks.SelectMany(t => t.Result).OrderBy(i => i).ToArray();

            Assert.Equal(expected, actual);
        }

        #endregion

        #region Projections

        [Fact]
        public async Task WriteProjectionIndexAsync_Always_Requires_Specific_Version()
        {
            await Assert.ThrowsAsync<UnexpectedStreamSequenceException>(() =>
                Store.WriteProjectionIndexAsync("a", ExpectedSequence.Any, new long[] { 1 })
            );
        }

        [Fact]
        public async Task WriteProjectionIndexAsync_Throws_On_Incorrect_Sequence()
        {
            await Assert.ThrowsAsync<UnexpectedStreamSequenceException>(() =>
                Store.WriteProjectionIndexAsync("a", 1, new long[] { 1 })
            );
        }

        [Fact]
        public async Task WriteProjectionIndexAsync_Throws_On_Expected_Sequence_None_With_Existing_Stream()
        {
            await Store.WriteProjectionIndexAsync("a", ExpectedSequence.None, new long[] { 1 });

            await Assert.ThrowsAsync<UnexpectedStreamSequenceException>(() =>
                Store.WriteProjectionIndexAsync("a", ExpectedSequence.None, new long[] { 2 })
            );
        }

        [Fact]
        public async Task WriteProjectionIndexAsync_Throws_On_Duplicated_Global_Sequence()
        {
            await Store.WriteProjectionIndexAsync("a", 0, new long[] { 1 });

            await Assert.ThrowsAsync<DuplicatedEntryException>(() =>
                Store.WriteProjectionIndexAsync("a", 1, new long[] { 1 })
            );
        }

        [Fact]
        public async Task ReadIndexedProjectionStreamAsync_Reads_Indexed_Events()
        {
            var sequencesToIndex = new long[] { 1, 3, 5, 7, 9 };

            await WriteAndProjectTestEvents(10, "a", sequencesToIndex);

            var indexedSequences = new List<long>();
            await Store.ReadIndexedProjectionStreamAsync("a", 0, EventCount.Unlimited, e => indexedSequences.Add(e.GlobalSequence), CancellationToken.None);

            Assert.Equal(sequencesToIndex, indexedSequences);
        }

        [Fact]
        public async Task ReadIndexedProjectionStreamAsync_Reads_No_Events_After_ClearIndexAsync()
        {
            var sequencesToIndex = new long[] { 1, 3, 5, 7, 9 };
            await WriteAndProjectTestEvents(10, "a", sequencesToIndex);

            await Store.ClearProjectionIndexAsync("a");

            var count = 0;

            await Store.ReadIndexedProjectionStreamAsync("a", 0, EventCount.Unlimited, e => count++, CancellationToken.None);

            Assert.Equal(0, count);
        }

        [Fact]
        public async Task ReadProjectionCheckpointAsync_With_Empty_Store_Returns_Zero()
        {
            var checkpoint = await Store.ReadProjectionCheckpointAsync("foo");

            Assert.Equal(0, checkpoint);
        }

        [Fact]
        public async Task ReadProjectionCheckpointAsync_Reads_WrittenValue()
        {
            await Store.WriteProjectionCheckpointAsync("a", 42);
            var checkpoint = await Store.ReadProjectionCheckpointAsync("a");

            Assert.Equal(42, checkpoint);
        }

        [Fact]
        public async Task ClearProjectionIndexAsync_Resets_Checkpoint_To_Zero()
        {
            await Store.WriteProjectionCheckpointAsync("a", 42);
            await Store.ClearProjectionIndexAsync("a");

            var checkpoint = await Store.ReadProjectionCheckpointAsync("a");

            Assert.Equal(0, checkpoint);
        }

        [Fact]
        public async Task ReadIndexedProjectionStreamAsync_Zero_Count_Should_Read_No_Events()
        {
            await WriteAndProjectTestEvents(1, "a", new long[] { 1 });

            await Store.ReadIndexedProjectionStreamAsync("a", 0, 0, e => { throw new Exception(); }, CancellationToken.None);
        }

        [Fact]
        public async Task ReadIndexedProjectionStreamAsync_Stops_Reading_If_Cancellation_Is_Requested()
        {
            await WriteAndProjectTestEvents(2, "a", new long[] { 1, 2 });

            var cts = new CancellationTokenSource();

            var count = 0;

            await Store.ReadIndexedProjectionStreamAsync("a", 0, EventCount.Unlimited, e =>
            {
                count++;
                cts.Cancel();

            }, cts.Token);

            Assert.Equal(1, count);
        }

        [Theory]
        [InlineData(0, 3, new long[] { 1, 2, 3 })]
        [InlineData(1, 3, new long[] { 1, 2, 3 })]
        [InlineData(2, 3, new long[] { 2, 3, 4 })]
        [InlineData(3, 3, new long[] { 3, 4, 5 })]
        [InlineData(4, 3, new long[] { 4, 5 })]
        [InlineData(5, 3, new long[] { 5 })]
        [InlineData(6, 3, new long[0])]
        public async Task ReadIndexedProjectionStreamAsync_Considers_Start_And_Count_Correctly(int start, int count, long[] expectedSequences)
        {
            await WriteAndProjectTestEvents(5, "a", new long[] { 1, 2, 3, 4, 5 });

            var actualSequences = new List<long>();
            await Store.ReadIndexedProjectionStreamAsync("a", start, count, e => actualSequences.Add(e.GlobalSequence), CancellationToken.None);

            Assert.Equal(expectedSequences, actualSequences);
        }

        [Fact]
        public async Task ReadHighestIndexedProjectionGlobalSequenceAsync_Returns_CorrectGlobalSequence()
        {
            await WriteTestEvents(10, "a");

            await Store.WriteProjectionIndexAsync("p1", 0, new long[] { 1, 2, 3 });
            await Store.WriteProjectionIndexAsync("p2", 0, new long[] { 3, 5, 7 });
            await Store.WriteProjectionIndexAsync("p3", 0, new long[] { 2, 4, 9 });

            var sequence = await Store.ReadHighestIndexedProjectionGlobalSequenceAsync("p2");

            Assert.Equal(7, sequence);
        }

        [Fact]
        public async Task ReadHighestIndexedProjectionStreamSequenceAsync_Returns_CorrectGlobalSequence()
        {
            await WriteAndProjectTestEvents(5, "a", new long[] { 1, 2, 3 });
            await WriteAndProjectTestEvents(5, "b", new long[] { 7, 8 });
            await WriteAndProjectTestEvents(5, "c", new long[] { 10, 11, 12 });

            var sequence = await Store.ReadHighestIndexedProjectionStreamSequenceAsync("b");

            Assert.Equal(2, sequence);
        }

        #endregion
    }
}
