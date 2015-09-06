using Akka.Actor;
using Akka.Event;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even
{
    public class EventProcessor : ReceiveActor
    {
        PersistedEventHandler _handlers = new PersistedEventHandler();

        public EventProcessor()
        {
            Receive<IPersistedEvent>(async e =>
            {
                await _handlers.Handle(e);
            });
        }

        protected void OnEvent<T>(Func<IPersistedEvent<T>, Task> handler)
        {
            Context.System.EventStream.Subscribe<IPersistedEvent<T>>(Self);
            _handlers.AddHandler<T>(e => handler((IPersistedEvent<T>) e));
        }

        protected void OnEvent<T>(Action<IPersistedEvent<T>> handler)
        {
            Context.System.EventStream.Subscribe<IPersistedEvent<T>>(Self);
            _handlers.AddHandler<T>(e => handler((IPersistedEvent<T>)e));
        }
    }
}
