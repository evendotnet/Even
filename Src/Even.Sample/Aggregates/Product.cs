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

            OnCommand<CreateProduct>(c => {

                if (State.IsCreated)
                    Fail("Product already exists");

                if (!State.IsDeleted)
                    Persist(new ProductCreated { Name = c.Name });
            });

            OnEvent<ProductCreated>((pe, e) => {
                State.IsCreated = true;
                State.Name = e.Name;
            });


            // renaming

            OnCommand<RenameProduct>(async c =>
            {
                var alreadyExists = await Task.FromResult(false);

                if (alreadyExists)
                    Fail("Can't rename, name already taken.");

                Persist(new ProductRenamed { NewName = c.NewName });
            });

            OnEvent<ProductRenamed>((pe, e) => {
                State.Name = e.NewName;
            });

            // deletion

            OnCommand<DeleteProduct>(c =>
            {
                if (State.IsCreated && !State.IsDeleted)
                    Persist(new ProductDeleted());
            });

            OnEvent<ProductDeleted>(e =>
            {
                State.IsDeleted = true;
            });
        }
    }
}
