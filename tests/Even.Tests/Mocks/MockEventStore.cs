﻿using Even.Persistence;
using NSubstitute;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Even.Internals;

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

            })).ReturnsForAnyArgs(Unit.GetCompletedTask());

            store.WriteAsync(Arg.Do<IReadOnlyCollection<IUnpersistedRawStreamEvent>>(events =>
            {
                int i = 1;

                foreach (var e in events)
                    e.GlobalSequence = i++;

            })).ReturnsForAnyArgs(Unit.GetCompletedTask());

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

                return Unit.GetCompletedTask();
            });

            var writeStreamCount = 0;

            store.WriteStreamAsync(null, 0, null).ReturnsForAnyArgs(t => {

                if (throwOnCalls.Contains(++writeStreamCount))
                    throw exception;

                return Unit.GetCompletedTask();
            });

            return store;
        }

        public static IEventStoreReader ThrowsOnReadStreams<T>()
            where T : Exception, new()
        {
            return ThrowsOnReadStreams(new T());
        }

        public static IEventStoreReader ThrowsOnReadStreams(Exception exception)
        {
            var store = Substitute.For<IEventStore>();

            store.ReadAsync(0, 0, null, default(CancellationToken))
                .ReturnsForAnyArgs(t =>
                {
                    throw exception;
                });

            store.ReadStreamAsync(null, 0, 0, null, default(CancellationToken))
                .ReturnsForAnyArgs(t =>
                {
                    throw exception;
                });

            store.ReadIndexedProjectionStreamAsync(null, 0, 0, null, default(CancellationToken))
                .ReturnsForAnyArgs(t =>
                {
                    throw exception;
                });

            return store;
        }
    }
}
