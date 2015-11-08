using Akka.Actor;
using System;

namespace Even
{
    public interface IQuery
    {
        IActorRef Sender { get; }
        object Message { get; }
        Timeout Timeout { get; }
    }

    public interface IQuery<T> : IQuery
    {
        new T Message { get; }
    }

    public class Query<T> : IQuery<T>
    {
        public Query(IActorRef sender, T query, Timeout timeout)
        {
            this.Sender = sender;
            this.Message = query;
            this.Timeout = timeout;
        }

        public IActorRef Sender { get; private set;  }
        public T Message { get; private set; }
        public Timeout Timeout { get; private set; }
        object IQuery.Message => Message;
    }

    public static class Query
    {
        public static IQuery Create(IActorRef sender, object query, TimeSpan timeout)
        {
            Argument.RequiresNotNull(query, nameof(query));

            var type = typeof(Query<>).MakeGenericType(query.GetType());

            return (IQuery) Activator.CreateInstance(type, sender, query, Timeout.In(timeout));
        }
    }
}
