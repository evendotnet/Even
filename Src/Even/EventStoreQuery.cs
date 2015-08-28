using Murmur;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;

namespace Even
{
    public class EventStoreQuery
    {
        public EventStoreQuery(IReadOnlyCollection<IStreamPredicate> predicates)
        {
            Contract.Requires(predicates != null && predicates.Any(), "Can't accept a null or empty predicate list.");
            this.Predicates = predicates;
        }

        private string _id;
        public string ID => _id ?? (_id = GenerateDeterministicID());
        public IReadOnlyCollection<IStreamPredicate> Predicates { get; }

        public bool IsStreamQuery => Predicates.Count == 1 && Predicates.First() is StreamQuery;

        protected string GenerateDeterministicID()
        {
            var items = Predicates
                .Select(q => JsonConvert.SerializeObject(q.GetDeterministicHashSource()))
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase);

            var str = String.Concat(items);
            var bytes = Encoding.UTF8.GetBytes(str);

            var ha = MurmurHash.Create128();
            var hash = ha.ComputeHash(bytes, 0, bytes.Length);

            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
    }
}
