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
                // this is used to retrieve the events later
                var memoryStore = new InMemoryStore();

                // initialize the event store
                var gateway = await actorSystem
                    .SetupEven()
                    .UseStore(memoryStore)
                    .AddProjection<ActiveProducts>()
                    .Start("even");

                // send some commands
                await Task.WhenAll(
                    gateway.SendAggregateCommand<Product>(1, new CreateProduct { Name = "Product 1" }),
                    gateway.SendAggregateCommand<Product>(2, new CreateProduct { Name = "Product 2" }),
                    gateway.SendAggregateCommand<Product>(3, new CreateProduct { Name = "Product 3" }),
                    gateway.SendAggregateCommand<Product>(2, new RenameProduct { NewName = "Product 2 - Renamed" }),
                    gateway.SendAggregateCommand<Product>(1, new DeleteProduct())
                );

                // add some delay to make sure the data is flushed to the store 
                await Task.Delay(100);

                // print the contents of the event store
                Console.WriteLine();
                Console.WriteLine("Event Store Data");
                Console.WriteLine("================");
                Console.WriteLine();
                Console.WriteLine($"{"Seq",-6} {"Stream ID",-50} Event Name");

                foreach (var e in memoryStore.GetEvents())
                    Console.WriteLine($"{e.GlobalSequence,-6} {e.StreamID,-50} {e.EventType}");

            }).Wait();

            Console.WriteLine();
            Console.WriteLine("End");
            Console.ReadLine();
        }
    }
}
