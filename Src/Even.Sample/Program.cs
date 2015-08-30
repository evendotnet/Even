using Akka.Actor;
using Even.Persistence;
using Even.Sample.Aggregates;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even.Sample
{
    class Program
    {
        static void Main(string[] args)
        {
            var actorSystem = ActorSystem.Create("Sample");

            var store = new InMemoryStorage();

            var gateway = actorSystem
                .SetupEventStore()
                .UseStorage(store)
                .Start();

            Task.Run(async () =>
            {
                await gateway.SendCommandAsync<Product>(1, new CreateProduct { Name = "Product 1" });
                await gateway.SendCommandAsync<Product>(2, new RenameProduct { NewName = "Product 1 - Renamed" });
                await gateway.SendCommandAsync<Product>(2, new CreateProduct { Name = "Product 2" });
                await gateway.SendCommandAsync<Product>(3, new CreateProduct { Name = "Product 2" });
                await gateway.SendCommandAsync<Product>(2, new DeleteProduct());

                await Task.Delay(1000);
            }).Wait();


            Console.WriteLine("Persisted Events: ");

            foreach (var e in store._store._events)
            {
                Console.WriteLine($"{e.StreamID,-20} {e.EventName,-30}");
            }

            Console.WriteLine("End");
            Console.ReadLine();
        }
    }
}
