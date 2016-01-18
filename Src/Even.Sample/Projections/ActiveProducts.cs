using Akka.Actor;
using Akka.Event;
using Even.Sample.Aggregates;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Contexts;
using System.Text;
using System.Threading.Tasks;

namespace Even.Sample.Projections
{
    public class ActiveProducts : PersistentProjection
    {
        private const string CN = "Server=localhost; Integrated Security=true; Database=Projections";

        protected override IProjectionStore Store { get; } = new Persistence.Sql.SqlServerProjectionStore(CN, "ActiveProducts", "ProductID");

        public ActiveProducts()
        {
            OnEvent<ProductCreated>(async e =>
            {
                await ProjectAsync(e.Stream.OriginalStreamName, new { e.DomainEvent.Name });
            });

            OnEvent<ProductRenamed>(async e =>
            {
                await ProjectAsync(e.Stream.OriginalStreamName, new { Name = e.DomainEvent.NewName });
            });

            OnEvent<ProductDeleted>(async e =>
            {
                await DeleteAsync(e.Stream.OriginalStreamName);
            });
        }

        protected override Task OnReceiveEvent(IPersistedStreamEvent e)
        {
            Console.WriteLine($"Projection Received Event {e.StreamSequence}: {e.EventType}");
            return Task.FromResult(1);
        }

        protected override void OnReady()
        {
            Receive<GetActiveProducts>(_ =>
            {
                var copy = _list.Select(i => i.Clone()).ToList();
                Sender.Tell(copy);
            });
        }

        List<ProductInfo> _list = new List<ProductInfo>();



        #region Event Processors

        private void Add(IPersistedEvent pe, ProductCreated e)
        {
            
        }

        private void Rename(IPersistedEvent pe, ProductRenamed e)
        {

        }

        private void Delete(IPersistedEvent pe, ProductDeleted e)
        {
           
        }

        #endregion
    }

    public class ProductInfo
    {
        public string ID { get; set; }
        public string Name { get; set; }

        public ProductInfo Clone()
        {
            return (ProductInfo)this.MemberwiseClone();
        }
    }

    public class GetActiveProducts
    { }
}
