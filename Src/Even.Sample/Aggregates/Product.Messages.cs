using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even.Sample.Aggregates
{
    // define commands
    public class CreateProduct { public string Name { get; set; } }
    public class RenameProduct { public string NewName { get; set; } }
    public class DeleteProduct { }

    // define events
    public class ProductCreated { public string Name { get; set; } }
    public class ProductRenamed { public string NewName { get; set; } }
    public class ProductDeleted { }
}
