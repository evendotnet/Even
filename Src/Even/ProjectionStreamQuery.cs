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
            return StreamHash.AsHashString(str);
        }
    }
}
