using Akka.Actor;
using Akka.Actor.Internal;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Even
{
    public static class QueryExtensions
    {
        public static Task<TResponse> Query<TResponse>(this ActorSystem system, object query, TimeSpan timeout)
        {
            return Query<TResponse>(system, null, query, timeout);
        }

        public static async Task<TResponse> Query<TResponse>(this ActorSystem system, ICanTell destination, object query, TimeSpan timeout)
        {
            var provider = (system as ActorSystemImpl)?.Provider;

            if (provider == null)
                throw new NotSupportedException("Unable to resolve the target Provider");

            var refs = AskRefs.Create(provider, timeout);
            var iquery = Even.Query.Create(refs.FutureActorRef, query, timeout);

            if (destination != null)
                destination.Tell(iquery, refs.FutureActorRef);
            else
                system.EventStream.Publish(iquery);

            object taskResult;

            try
            {
                taskResult = await refs.CompletionSource.Task;
            }
            catch (TaskCanceledException ex)
            {
                throw new QueryException("Query timeout.", ex);
            }
            catch (Exception ex)
            {
                throw new QueryException("Unexpected query exception.", ex);
            }

            return (TResponse)taskResult;
        }
    }

    public class AskRefs
    {
        public FutureActorRef FutureActorRef { get; }
        public TaskCompletionSource<object> CompletionSource { get; }

        public static AskRefs Create(IActorRefProvider provider, TimeSpan timeout)
        {
            var tcs = new TaskCompletionSource<object>();

            // logic copied from Ask (Akka.Actor -> Futures.cs)

            if (timeout != System.Threading.Timeout.InfiniteTimeSpan && timeout > default(TimeSpan))
            {
                var cancellationSource = new CancellationTokenSource();
                cancellationSource.Token.Register(() => tcs.TrySetCanceled());
                cancellationSource.CancelAfter(timeout);
            }

            //create a new tempcontainer path
            ActorPath path = provider.TempPath();

            //callback to unregister from tempcontainer
            Action unregister = () => provider.UnregisterTempActor(path);
            var future = new FutureActorRef(tcs, unregister, path);

            //The future actor needs to be registered in the temp container
            provider.RegisterTempActor(future, path);

            return new AskRefs(future, tcs);
        }

        public AskRefs(FutureActorRef futureRef, TaskCompletionSource<object> completionSource)
        {
            FutureActorRef = futureRef;
            CompletionSource = completionSource;
        }
    }
}
