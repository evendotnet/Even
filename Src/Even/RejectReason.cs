using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Even
{
    [DebuggerDisplay("FirstReason")]
    public class RejectReasons : IEnumerable<KeyValuePair<string, string>>
    {
        private Dictionary<string, List<string>> _reasons = new Dictionary<string, List<string>>(StringComparer.InvariantCultureIgnoreCase);

        public RejectReasons()
        { }

        public RejectReasons(string reason)
        {
            Add(reason);
        }

        public string FirstReason => this.Any() ? this.First().Value : null;

        public void Add(string reason)
        {
            Add(String.Empty, reason);
        }

        public void Add(string key, string reason)
        {
            Argument.RequiresNotNull(key, nameof(key));

            List<string> list;

            if (!_reasons.TryGetValue(key, out list))
            {
                list = new List<string>();
                _reasons.Add(key, list);
            }

            list.Add(reason);
        }

        public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
        {
            foreach (var kvp in _reasons)
            {
                foreach (var s in kvp.Value)
                {
                    yield return new KeyValuePair<string, string>(kvp.Key, s);
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
