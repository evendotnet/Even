using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;

namespace Even
{
    /// <summary>
    /// Represents a projection stream query.
    /// </summary>
    public class ProjectionStreamQuery
    {
        public ProjectionStreamQuery(IReadOnlyCollection<IProjectionStreamPredicate> predicates)
        {
            this.Predicates = predicates ?? new IProjectionStreamPredicate[0];
        }

        private string _id;

        /// <summary>
        /// A deterministic ID that will always be the same for the same query.
        /// </summary>
        public string ProjectionStreamID => _id ?? (_id = GenerateID());
        public IReadOnlyCollection<IProjectionStreamPredicate> Predicates { get; }

        private string GenerateID()
        {
            var items = Predicates
                .Select(q => JsonConvert.SerializeObject(q.GetDeterministicHashSource()))
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase);

            var str = String.Concat(items);
            var bytes = Encoding.UTF8.GetBytes(str);

            var ha = new System.Security.Cryptography.SHA1CryptoServiceProvider();
            var hash = ha.ComputeHash(bytes, 0, bytes.Length);

            var sb = new StringBuilder(bytes.Length * 2);

            foreach (var b in hash)
                sb.Append(b.ToString("x2"));

            return sb.ToString().ToLowerInvariant();
        }
    }
}
