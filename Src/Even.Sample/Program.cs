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
                    .SetupEven()
                    .AddProjection<ActiveProducts>()
                    .Start();

                await Task.Delay(500);

                await Task.WhenAll(
                    gateway.SendAggregateCommand<Product>(1, new CreateProduct { Name = "Product 1" }),
                    gateway.SendAggregateCommand<Product>(2, new CreateProduct { Name = "Product 2" }),
                    gateway.SendAggregateCommand<Product>(3, new CreateProduct { Name = "Product 2" }),
                    gateway.SendAggregateCommand<Product>(2, new RenameProduct { NewName = "Product 1 - Renamed" }),
                    gateway.SendAggregateCommand<Product>(1, new DeleteProduct())
                );

                await Task.Delay(500);

                Console.WriteLine($"{"CP",-6} {"Stream ID",-50} Event Name");
                foreach (var e in memoryStore.GetEvents())
                    Console.WriteLine($"{e.GlobalSequence,-6} {e.StreamID,-50} {e.EventType}");


            }).Wait();

            Console.WriteLine("End");
            Console.ReadLine();
        }
    }
}
