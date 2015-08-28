using Akka.Actor;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even
{
    public class EventProcessor : ReceiveActor
    {
        private Dictionary<Type, Action<object>> _eventHandlers = new Dictionary<Type, Action<object>>();

        public void ProcessEvent<T>(Action<T> processor)
        {
            Contract.Requires(processor != null);
            _eventHandlers.Add(typeof(T), evt => processor((T)evt));
        }

        //protected override bool Receive(object message)
        //{
        //    if (message == null)
        //        return false;

        //    var messageType = message.GetType();

        //    Action<object> processor;
        //}
    }
}
