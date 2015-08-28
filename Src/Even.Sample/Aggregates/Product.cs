using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even.Sample.Aggregates
{
    // define the state
    public class ProductState
    {
        public bool IsCreated { get; set; }
        public bool IsDeleted { get; set; }
        public string Name { get; set; }
    }

    // define the aggregate 
    public class Product : Aggregate<ProductState>
    {
        public Product()
        {
            // creation

            ProcessCommand<CreateProduct>(c => {

                if (State.IsCreated)
                    Fail("Product already exists");

                if (!State.IsDeleted)
                    Persist(new ProductCreated { Name = c.Name });
            });

            ProcessEvent<ProductCreated>(e => {
                State.IsCreated = true;
                State.Name = e.Name;
            });


            // renaming

            ProcessCommand<RenameProduct>(async c =>
            {
                var alreadyExists = await Task.FromResult(false);

                if (alreadyExists)
                    Fail("Can't rename, name already taken.");

                Persist(new ProductRenamed { NewName = c.NewName });
            });

            ProcessEvent<ProductRenamed>(e => {
                State.Name = e.NewName;
            });

            // deletion

            ProcessCommand<DeleteProduct>(c =>
            {
                if (State.IsCreated && !State.IsDeleted)
                    Persist(new ProductDeleted());
            });

            ProcessEvent<ProductDeleted>(e =>
            {
                State.IsDeleted = true;
            });
        }
    }
}
