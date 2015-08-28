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

        private void Add(IStreamEvent se, ProductCreated e)
        {
            _list.Add(new ProductInfo { ID = se.StreamID, Name = e.Name });
        }

        private void Rename(IStreamEvent se, ProductRenamed e)
        {
            var pi = _list.FirstOrDefault(i => i.ID == se.StreamID);
            
            if (pi != null)
                pi.Name = e.NewName;
        }

        private void Delete(IStreamEvent se)
        {
            _list.RemoveAll(i => i.ID == se.StreamID);
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
