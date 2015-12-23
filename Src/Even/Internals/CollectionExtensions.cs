using System;
using System.Collections;
using System.Collections.Generic;

namespace Even.Internals
{
    internal static class CollectionExtensions
    {
        public static IReadOnlyCollection<T> AsReadOnlyCollection<T>(this LinkedList<T> @this)
        {
            if (@this == null) throw new ArgumentNullException(nameof(@this));
            return new ReadOnlyLinkedListAdapter<T>(@this);
        }

        private class ReadOnlyLinkedListAdapter<T>:IReadOnlyCollection<T>
        {
            private readonly LinkedList<T> _items;

            public ReadOnlyLinkedListAdapter(LinkedList<T> items)
            {
                _items = items;
            }

            public IEnumerator<T> GetEnumerator()
            {
                return _items.GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            public int Count
            {
                get
                {
                    return _items.Count;
                }
            }
        }
    }
}