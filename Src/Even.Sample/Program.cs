using Akka.Actor;
using Even.Persistence;
using Even.Sample.Aggregates;
using Even.Sample.Projections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Even.Sample
{
    class Program
    {
        static void Main(string[] args)
        {
            var actorSystem = ActorSystem.Create("Sample");

            Task.Run(async () =>
            {
                var memoryStore = new InMemoryStore();

                var gateway = await actorSystem
                    .SetupEventStore()
                    .UseStore(memoryStore)
                    .AddEventProcessor<ActiveProducts>()
                    .Start();

                await Task.WhenAll(
                    gateway.SendCommandAsync<Product>(1, new CreateProduct { Name = "Product 1" }),
                    gateway.SendCommandAsync<Product>(2, new RenameProduct { NewName = "Product 1 - Renamed" }),
                    gateway.SendCommandAsync<Product>(2, new CreateProduct { Name = "Product 2" }),
                    gateway.SendCommandAsync<Product>(3, new CreateProduct { Name = "Product 2" }),
                    gateway.SendCommandAsync<Product>(2, new DeleteProduct())
                );

                await Task.Delay(500);

                Console.WriteLine($"{"CP",-6} {"Stream ID",-50} Event Name");
                foreach (var e in memoryStore.GetEvents())
                    Console.WriteLine($"{e.Checkpoint,-6} {e.StreamID,-50} {e.EventName}");


            }).Wait();

            Console.WriteLine("End");
            Console.ReadLine();
        }
    }
}
