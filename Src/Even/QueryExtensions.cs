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

        //public static Task<TResponse> Query<TResponse>(this IActorRefFactory factory, object query, TimeSpan timeout)
        //{
        //    return Query<TResponse>(factory, null, query, timeout);
        //}

        //public static async Task<TResponse> Query<TResponse>(this IActorRefFactory factory, string actorPath, object query, TimeSpan timeout)
        //{
        //    var provider = ResolveProvider(factory);

        //    if (provider == null)
        //        throw new NotSupportedException("Unable to resolve the target Provider");

        //    var refs = AskRefs.Create(provider, timeout);
        //    var iquery = Even.Query.Create(refs.FutureActorRef, query, timeout);

        //    if (actorPath != null)
        //        factory.ActorSelection(actorPath).Tell(query, refs.FutureActorRef);
        //    else
        //    {
        //        //var actor = factory.ActorOf(QueryAsker.Props);
        //        //var iquery = Even.Query.Create(target, query, timeout);
        //        //var esQuery = new QueryAsker.StartQuery(iquery, timeout);
        //        //askResult = actor.Ask(esQuery, timeout);
        //    }

        //    object result;

        //    try
        //    {
        //        result = await askResult;
        //    }
        //    catch (Exception ex)
        //    {
        //        throw new QueryException("Error waiting for the query response.", ex);
        //    }

        //    return default(TResponse);
        //}

        //static IActorRefProvider ResolveProvider(IActorRefFactory factory)
        //{
        //    if (factory is ActorSystemImpl)
        //        return ((ActorSystemImpl)factory).Provider;


        //    //if (ActorCell.Current != null)
        //    //    return InternalCurrentActorCellKeeper.Current.SystemImpl.Provider;

        //    //if (self is IInternalActorRef)
        //    //    return self.AsInstanceOf<IInternalActorRef>().Provider;

        //    //if (self is ActorSelection)
        //    //    return ResolveProvider(self.AsInstanceOf<ActorSelection>().Anchor);

        //    return null;
        //}

        //private static TaskCompletionSource<object> CreateFutureActorRef(IActorRefProvider provider, TimeSpan timeout)
        //{
        //    var result = new TaskCompletionSource<object>(TaskContinuationOptions.AttachedToParent);

        //    if (timeout != System.Threading.Timeout.InfiniteTimeSpan && timeout > default(TimeSpan))
        //    {
        //        var cancellationSource = new CancellationTokenSource();
        //        cancellationSource.Token.Register(() => result.TrySetCanceled());
        //        cancellationSource.CancelAfter(timeout);
        //    }

        //    //create a new tempcontainer path
        //    ActorPath path = provider.TempPath();
        //    //callback to unregister from tempcontainer
        //    Action unregister = () => provider.UnregisterTempActor(path);
        //    var future = new FutureActorRef(result, unregister, path);
        //    //The future actor needs to be registered in the temp container
        //    provider.RegisterTempActor(future, path);

        //    return result;
        //}
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

    ///// <summary>
    ///// Sends a query through the event stream and responds with the first message it receives back.
    ///// </summary>
    //internal class QueryAsker : ReceiveActor
    //{
    //    public static readonly Props Props = Props.Create<QueryAsker>();

    //    IActorRef _sender;

    //    public QueryAsker()
    //    {
    //        Receive<StartQuery>(esQuery =>
    //        {
    //            _sender = Sender;
    //            Context.System.EventStream.Publish(esQuery.Query);

    //            Become(() =>
    //            {
    //                SetReceiveTimeout(esQuery.Timeout);

    //                Receive<ReceiveTimeout>(_ =>
    //                {
    //                    Context.Stop(Self);
    //                });

    //                ReceiveAny(o =>
    //                {
    //                    _sender.Forward(o);
    //                    Context.Stop(Self);
    //                });
    //            });
    //        });
    //    }

    //    public class StartQuery
    //    {
    //        public StartQuery(IQuery query, TimeSpan timeout)
    //        {
    //            this.Query = query;
    //            this.Timeout = timeout;
    //        }

    //        public IQuery Query { get; }
    //        public TimeSpan Timeout { get; }
    //    }
    //}
}
