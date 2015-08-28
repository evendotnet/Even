using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even
{
    public interface IStorageDriver
    {
        IStorageReader CreateReader();
        IStorageWriter CreateWriter();
    }
}
