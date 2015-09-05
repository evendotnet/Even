using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even
{
    [AttributeUsage(AttributeTargets.Class)]
    public class EvenStorageFormatAttribute : Attribute
    {
        public EvenStorageFormatAttribute(int format)
        {
            this.StorageFormat = format;
        }

        public int StorageFormat { get; }

        public static int GetStorageFormat(Type type)
        {
            if (type == null)
                return 0;

            var attr = type.GetCustomAttributes(typeof(EvenStorageFormatAttribute), false).FirstOrDefault() as EvenStorageFormatAttribute;

            return attr?.StorageFormat ?? 0;
        }
    }
}
