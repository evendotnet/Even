using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even.Persistence
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
        /// <param name="onItem">The action to append the item to the string.</param>
        /// <param name="onFinish">(Optional) An action to run when the limit is reached or the list is over, or null for none.</param>
        /// <returns>An enumerable with each built string.</returns>
        public static IEnumerable<string> Build<T>(IEnumerable<T> items, int maxBatchSize, int desiredLength, Action<StringBuilder> onStart, Action<StringBuilder, T> onItem, Action<StringBuilder> onFinish)
        {
            Contract.Requires(items != null);
            Contract.Requires(onItem != null);
            Contract.Requires(maxBatchSize > 0);
            Contract.Requires(desiredLength > 0);

            var sb = new StringBuilder();
            int remaining = maxBatchSize;

            foreach (var i in items)
            {
                remaining--;

                // if the string is empty
                if (sb.Length == 0)
                {
                    if (onStart != null)
                        onStart(sb);

                    // always append at least one item
                    onItem(sb, i);

                    // if no more items, yield and go to next
                    if (remaining == 0)
                    {
                        if (onFinish != null)
                            onFinish(sb);

                        yield return sb.ToString();

                        sb.Clear();
                        remaining = maxBatchSize;
                    }

                    continue;
                }

                var previousLength = sb.Length;

                onItem(sb, i);

                // if it exceeds the length
                if (sb.Length > desiredLength)
                {
                    // remove the appeneded item from the current string
                    var lastInsertLength = sb.Length - previousLength;
                    var tmp = sb.ToString(previousLength, lastInsertLength);
                    sb.Length = previousLength;

                    // wrap up and yield
                    if (onFinish != null)
                        onFinish(sb);

                    yield return sb.ToString();

                    // start a new string and append the generated value
                    sb.Clear();

                    if (onStart != null)
                        onStart(sb);

                    sb.Append(tmp);
                    remaining = maxBatchSize - 1;
                    continue;
                }

                // if the limit is reached, yield
                if (remaining == 0 || sb.Length == desiredLength)
                {
                    if (onFinish != null)
                        onFinish(sb);

                    yield return sb.ToString();

                    sb.Clear();
                    remaining = maxBatchSize;

                    continue;
                }
            }

            // if its the end of the list, see if there is anything to return
            if (sb.Length > 0)
            {
                if (onFinish != null)
                    onFinish(sb);

                yield return sb.ToString();
            }
        }
    }
}
