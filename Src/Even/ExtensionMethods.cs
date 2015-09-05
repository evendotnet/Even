using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even
{
    internal static class LinkedListExtensions
    {
        public static IEnumerable<LinkedListNode<T>> Nodes<T>(this LinkedList<T> list)
        {
            var node = list.First;

            while (node != null)
            {
                yield return node;
                node = node.Next;
            }
        }
    }
}
