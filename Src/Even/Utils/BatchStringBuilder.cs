using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even.Utils
{
    public static class BatchStringBuilder
    {
        /// <summary>
        /// Builds large strings from enumerables by splitting them into batches taking into account 
        /// the maximum number of items and a maximum string length.
        /// </summary>
        /// <param name="items">The list of items to enumerate.</param>
        /// <param name="maxBatchSize">The maximum number of items to append to each string.</param>
        /// <param name="desiredLength">The desired maximum string length. This is a soft limit.
        /// At least one item will always be appended to the string, and it does not take into account
        /// modifications made by <see cref="onFinish"/>.</param>
        /// <param name="onStart">(Optional) An action to run when building a new string, or null for none.</param>
        /// <param name="onItem">(Optional) The action to append the item to the string, or null for none. If null, the item will be appended as item.ToString().</param>
        /// <param name="onFinish">(Optional) An action to run when the limit is reached or the list is over, or null for none.</param>
        /// <returns>An enumerable with each built string and the items that compose it.</returns>
        public static IEnumerable<BatchStringBuilderOutput<T>> Build<T>(IEnumerable<T> items, int maxBatchSize = Int32.MaxValue, int desiredLength = Int32.MaxValue, Action<StringBuilder> onStart = null, Action<StringBuilder, T> onItem = null, Action<StringBuilder> onFinish = null)
        {
            Contract.Requires(items != null);
            Contract.Requires(maxBatchSize > 0);
            Contract.Requires(desiredLength > 0);

            onItem = onItem ?? new Action<StringBuilder, T>((sb, i) => sb.Append(i));
            var generator = new Generator<T>(onStart, onItem, onFinish);

            var remaining = maxBatchSize;

            foreach (var i in items)
            {
                remaining--;

                // if the string is empty
                if (generator.Length == 0)
                {
                    generator.Append(i);

                    // if no more items, yield and go to next
                    if (remaining == 0)
                    {
                        yield return generator.OutputAndReset();

                        remaining = maxBatchSize;
                    }

                    continue;
                }

                var previousLength = generator.Length;

                generator.Append(i);

                // if it exceeds the length
                if (generator.Length > desiredLength)
                {
                    yield return generator.OutputWithoutLastAppendedAndReset();
                    remaining = maxBatchSize - 1;

                    continue;
                }

                // if the limit is reached, yield
                if (remaining == 0 || generator.Length == desiredLength)
                {
                    yield return generator.OutputAndReset();
                    remaining = maxBatchSize;

                    continue;
                }
            }

            // if its the end of the list, see if there is anything to return
            if (generator.AppendedAny)
            {
                yield return generator.OutputAndReset();
            }
        }

        class Generator<T>
        {
            LinkedList<T> items = new LinkedList<T>();
            StringBuilder sb = new StringBuilder();
            Action<StringBuilder> onStart;
            Action<StringBuilder> onFinish;
            Action<StringBuilder, T> onItem;
            int _previousLength;

            public Generator(Action<StringBuilder> onStart, Action<StringBuilder, T> onItem, Action<StringBuilder> onFinish)
            {
                this.onStart = onStart;
                this.onItem = onItem;
                this.onFinish = onFinish;
            }

            public int Length => sb.Length;
            public bool AppendedAny { get; private set; }

            public void Append(T item)
            {
                AppendedAny = true;
                _previousLength = sb.Length;

                if (sb.Length == 0 && onStart != null)
                    onStart(sb);

                items.AddLast(item);
                onItem(sb, item);
            }

            public BatchStringBuilderOutput<T> OutputAndReset()
            {
                if (onFinish != null)
                    onFinish(sb);

                var appended = items;
                var str = sb.ToString();

                items = new LinkedList<T>();
                sb.Clear();
                AppendedAny = false;

                return new BatchStringBuilderOutput<T>(appended, str);
            }

            public BatchStringBuilderOutput<T> OutputWithoutLastAppendedAndReset()
            {
                var lastItem = items.Last.Value;
                items.RemoveLast();
                sb.Length = _previousLength;

                var output = OutputAndReset();

                Append(lastItem);

                return output;
            }
        }
    }

    public class BatchStringBuilderOutput<T>
    {
        public BatchStringBuilderOutput(IReadOnlyCollection<T> items, string str)
        {
            this.Items = items;
            this.String = str;
        }

        public IReadOnlyCollection<T> Items { get; }
        public string String { get; }
    }
}
