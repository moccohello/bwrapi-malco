using System;
using System.Collections.Generic;
using System.Linq;

namespace Malco.Presentation.Spatial
{
    internal sealed class SpatialIdentityReconciler
    {
        private HashSet<string> _lineIds = new HashSet<string>(StringComparer.Ordinal);
        private HashSet<string> _gasIds = new HashSet<string>(StringComparer.Ordinal);
        private HashSet<string> _mineralIds = new HashSet<string>(StringComparer.Ordinal);

        public SpatialIdentityChanges ReconcileLines(IEnumerable<string> ids) => Reconcile(ref _lineIds, ids);
        public SpatialIdentityChanges ReconcileGas(IEnumerable<string> ids) => Reconcile(ref _gasIds, ids);
        public SpatialIdentityChanges ReconcileMinerals(IEnumerable<string> ids) => Reconcile(ref _mineralIds, ids);

        public void Clear()
        {
            _lineIds.Clear();
            _gasIds.Clear();
            _mineralIds.Clear();
        }

        private static SpatialIdentityChanges Reconcile(ref HashSet<string> current, IEnumerable<string> ids)
        {
            var next = new HashSet<string>((ids ?? Array.Empty<string>()).Where(id => !string.IsNullOrEmpty(id)), StringComparer.Ordinal);
            var previous = current;
            if (previous.SetEquals(next)) return SpatialIdentityChanges.Unchanged;
            var added = next.Where(id => !previous.Contains(id)).ToArray();
            var removed = previous.Where(id => !next.Contains(id)).ToArray();
            current = next;
            return new SpatialIdentityChanges(added, removed);
        }
    }

    internal sealed class SpatialIdentityChanges
    {
        public static readonly SpatialIdentityChanges Unchanged =
            new SpatialIdentityChanges(Array.Empty<string>(), Array.Empty<string>());

        public SpatialIdentityChanges(IReadOnlyList<string> added, IReadOnlyList<string> removed)
        {
            Added = added ?? Array.Empty<string>();
            Removed = removed ?? Array.Empty<string>();
        }

        public IReadOnlyList<string> Added { get; }
        public IReadOnlyList<string> Removed { get; }
        public bool HasIdentityChanges => Added.Count != 0 || Removed.Count != 0;
    }
}
