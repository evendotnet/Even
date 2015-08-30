using Akka.Actor;
using Even.Sample.Aggregates;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even.Sample.Projections
{
    public class ActiveProducts : Projection
    {
        public ActiveProducts()
        {
            ProcessEvent<ProductCreated>(Add);
            ProcessEvent<ProductRenamed>(Rename);
            ProcessEvent<ProductDeleted>(Delete);
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

        private void Add(IEvent e, ProductCreated pe)
        {
            _list.Add(new ProductInfo { ID = e.StreamID, Name = pe.Name });
        }

        private void Rename(IEvent e, ProductRenamed pe)
        {
            var pi = _list.FirstOrDefault(i => i.ID == e.StreamID);
            
            if (pi != null)
                pi.Name = pe.NewName;
        }

        private void Delete(IEvent e)
        {
            _list.RemoveAll(i => i.ID == e.StreamID);
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
