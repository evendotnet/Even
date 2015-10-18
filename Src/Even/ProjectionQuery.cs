using System;

namespace Even
{
    public interface IQuery
    {
        object Message { get; }
        Timeout Timeout { get; }
    }

    public interface IQuery<T> : IQuery
    {
        new T Message { get; }
    }

    public class Query<T> : IQuery<T>
    {
        public Query(T query, Timeout timeout)
        {
            this.Message = query;
            this.Timeout = timeout;
        }

        public T Message { get; private set; }
        public Timeout Timeout { get; private set; }
        object IQuery.Message => Message;
    }

    public static class QueryFactory
    {
        public static IQuery Create(object query, Timeout timeout)
        {
            Argument.RequiresNotNull(query, nameof(query));
            Argument.RequiresNotNull(timeout, nameof(timeout));

            var type = typeof(Query<>).MakeGenericType(query.GetType());

            return (IQuery) Activator.CreateInstance(type, query, timeout);
        }
    }
}
