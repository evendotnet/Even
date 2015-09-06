using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even
{
    public class MessageHandler<TMessage>
    {
        public MessageHandler(Func<TMessage, object> mapper)
        {
            _mapper = mapper;
        }

        Func<TMessage, object> _mapper;
        class HandlerList : LinkedList<Func<TMessage, Task>> { }

        Dictionary<Type, HandlerList> _handlers = new Dictionary<Type, HandlerList>();

        public async Task Handle(TMessage message)
        {
            if (message == null)
                return;

            Type type;

            if (_mapper != null)
            {
                type = _mapper(message)?.GetType();

                if (type == null)
                    return;
            }
            else
            {
                type = typeof(TMessage);
            }

            HandlerList list;

            if (_handlers.TryGetValue(type, out list))
            {
                foreach (var handler in list)
                    await handler(message);
            }
        }

        public void AddHandler<T>(Func<TMessage, Task> handler)
        {
            HandlerList list;

            if (!_handlers.TryGetValue(typeof(T), out list))
            {
                list = new HandlerList();
                _handlers.Add(typeof(T), list);
            }

            list.AddLast(handler);
        }

        public void AddHandler<T>(Action<TMessage> handler)
        {
            AddHandler<T>(msg =>
            {
                handler(msg);
                return Task.CompletedTask;
            });
        }
    }

    public class PersistedEventHandler : MessageHandler<IPersistedEvent>
    {
        public PersistedEventHandler()
            : base(e => e.DomainEvent)
        { }
    }

    public class ProjectionEventHandler : MessageHandler<IProjectionEvent>
    {
        public ProjectionEventHandler()
            : base(e => e.DomainEvent)
        { }
    }
}
