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
    public class ActiveProducts : Projection
    {
        public ActiveProducts()
        {
            OnEvent<ProductCreated>(e =>
            {
                _list.Add(new ProductInfo { ID = e.StreamID, Name = e.DomainEvent.Name });
            });

            OnEvent<ProductDeleted>(e =>
            {
                _list.RemoveAll(i => i.ID == e.StreamID);
            });
        }

        protected override Task OnReceiveEvent(IPersistedStreamEvent e)
        {
            Console.WriteLine($"Projection Received Event {e.StreamSequence}: {e.EventType}");
            return Task.CompletedTask;
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
