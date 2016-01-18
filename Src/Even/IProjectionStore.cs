using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even
{
    public interface IProjectionStore
    {
        Task InitializeAsync();
        Task ProjectAsync(object key, object value);
        Task DeleteAsync(object key);
        Task DeleteAllAsync();

        Task<ProjectionState> GetStateAsync();
    }
}
