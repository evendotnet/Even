using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class ESCategoryAttribute : Attribute
    {
        public ESCategoryAttribute(string category)
        {
            Contract.Requires(!String.IsNullOrWhiteSpace(category));

            this.Category = Category;
        }

        public string Category { get; }

        public static string GetCategory(Type type)
        {
            var a = Attribute.GetCustomAttribute(type, typeof(ESCategoryAttribute)) as ESCategoryAttribute;
            return a?.Category ?? type.Name.ToLowerInvariant();
        }
    }
}
