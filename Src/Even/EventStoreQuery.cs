using Murmur;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;

namespace Even
{
    /// <summary>
    /// Represents a query to the event store.
    /// </summary>
    public class ProjectionQuery
    {
        public ProjectionQuery(IReadOnlyCollection<IProjectionStreamPredicate> predicates)
        {
            this.Predicates = predicates ?? new IProjectionStreamPredicate[0];
        }

        private string _id;

        /// <summary>
        /// A deterministic ID that will always be the same for the same query.
        /// </summary>
        public string StreamID => _id ?? (_id = GenerateDeterministicID());
        public IReadOnlyCollection<IProjectionStreamPredicate> Predicates { get; }

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

        public QueryHint CreateHint()
        {
            return null;
        }
    }
}
