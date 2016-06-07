using Akka.TestKit.Xunit2;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even.Tests
{
    public class EvenTestKit : TestKit
    {
        static readonly string Config = File.ReadAllText("TestConfig.hocon");

        public EvenTestKit()
            : base(Config)
        { }

        public T ExpectMsgEventually<T>(Predicate<T> isMessage = null, TimeSpan? timeout = null)
        {
            var to = GetTimeoutOrDefault(timeout);

            var received = Within<T>(to, () =>
            {
                do
                {
                    var msg = ExpectMsg<object>(Remaining);

                    if (msg is T)
                        return (T)msg;

                } while (Remaining > TimeSpan.Zero);

                throw new TimeoutException();
            });

            if (isMessage == null)
                return received;

            if (isMessage(received))
                return received;

            throw new Exception($"Message of type '{typeof(T).FullName}' received, but didn't match the predicate");
        }
    }
}
