using Akka.Actor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even
{
    public static class PropsFactory
    {
        static Func<Type, Props> _factory;

        private static void SetProducer(Func<Type, Props> factory) => _factory = factory;

        public static Props Create(Type type)
        {
            if (_factory != null)
                return _factory(type);

            return Props.Create(type);
        }

        public static Props Create<T>()
        {
            return Create(typeof(T));
        }

        public static Props Create<T>(params object[] parameters)
        {
            return Props.Create(typeof(T), parameters);
        }
    }
}
