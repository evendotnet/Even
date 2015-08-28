using Akka.Actor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even
{
    public static class ActorOfExtensions
    {
        public static IActorRef CreateActor<T>(this IActorRefFactory context, string name, params object[] parameters)
        {
            var props = PropsFactory.Create<T>(parameters);
            return context.ActorOf(props, name);
        }

        public static IActorRef CreateActor<T>(this IActorRefFactory context, string name)
        {
            return CreateActor(context, name, typeof(T));
        }

        public static IActorRef CreateActor(this IActorRefFactory context, string name, Type type)
        {
            var props = PropsFactory.Create(type);
            return context.ActorOf(props, name);
        }
    }
}
