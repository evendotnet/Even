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
    public class ProjectionStreamQuery : IEquatable<ProjectionStreamQuery>
    {
        public ProjectionStreamQuery(params IProjectionStreamPredicate[] predicates)
        {
            _predicates = predicates ?? new IProjectionStreamPredicate[0];
        }

        public ProjectionStreamQuery(IEnumerable<IProjectionStreamPredicate> predicates)
            : this(predicates.ToArray())
        { }

        private Stream _stream;
        private IProjectionStreamPredicate[] _predicates;

        /// <summary>
        /// A deterministic stream that will always be the same for the same query.
        /// </summary>
        public Stream ProjectionStream => _stream ?? (_stream = CreateStream());

        [Obsolete]
        public IReadOnlyCollection<IProjectionStreamPredicate> Predicates => _predicates;

        private Stream CreateStream()
        {
            var items = _predicates
                .Select(q => JsonConvert.SerializeObject(q.GetDeterministicHashSource()))
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase);

            var bytes = Encoding.UTF8.GetBytes(String.Concat(items));

            return Stream.FromBytes(bytes);
        }

        public bool EventMatches(IPersistedEvent e)
        {
            foreach (var p in _predicates)
                if (p.EventMatches(e))
                    return true;

            return false;
        }

        public bool Equals(ProjectionStreamQuery other)
        {
            return other != null && ProjectionStream == other.ProjectionStream;
        }
    }
}
