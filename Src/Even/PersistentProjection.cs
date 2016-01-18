using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even
{
    public abstract class PersistentProjection : Projection
    {
        protected abstract IProjectionStore Store { get; }

        protected override Task OnInit()
        {
            return Store.InitializeAsync();
        }

        public virtual Task ProjectAsync(object key, object value)
        {
            return Store.ProjectAsync(key, value);
        }

        public virtual Task DeleteAsync(object key)
        {
            return Store.DeleteAsync(key);
        }

        protected override Task PrepareToRebuild()
        {
            return Store.DeleteAllAsync();
        }

        protected override Task<ProjectionState> GetLastKnownState()
        {
            return Store.GetStateAsync();
        }
    }
}
