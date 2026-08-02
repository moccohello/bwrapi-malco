using System.Collections.Generic;
using System.Linq;

namespace Malco.Data
{
    internal sealed class WorkerResourceAssignmentTracker<TKey>
    {
        private sealed class Assignment
        {
            public string ResourceKey { get; set; }
            public long LastObservedTicks { get; set; }
        }

        private readonly Dictionary<TKey, Assignment> _assignments;

        public WorkerResourceAssignmentTracker(IEqualityComparer<TKey> comparer = null)
        {
            _assignments = new Dictionary<TKey, Assignment>(comparer ?? EqualityComparer<TKey>.Default);
        }

        public bool TryGet(TKey workerKey, out string resourceKey)
        {
            long ignoredObservedTicks;
            return TryGet(workerKey, out resourceKey, out ignoredObservedTicks);
        }

        public bool TryGet(TKey workerKey, out string resourceKey, out long lastObservedTicks)
        {
            Assignment assignment;
            if (_assignments.TryGetValue(workerKey, out assignment))
            {
                resourceKey = assignment.ResourceKey;
                lastObservedTicks = assignment.LastObservedTicks;
                return true;
            }

            resourceKey = null;
            lastObservedTicks = 0;
            return false;
        }

        public void Set(TKey workerKey, string resourceKey, long observedTicks)
        {
            _assignments[workerKey] = new Assignment
            {
                ResourceKey = resourceKey,
                LastObservedTicks = observedTicks
            };
        }

        public void Remove(TKey workerKey) => _assignments.Remove(workerKey);
        public void Clear() => _assignments.Clear();

        public IReadOnlyCollection<TKey> SnapshotWorkerKeys()
        {
            return _assignments.Keys.ToArray();
        }

        public void PruneMissing(ISet<TKey> observedWorkerKeys, long observedTicks, long retentionTicks)
        {
            foreach (var workerKey in _assignments.Keys.ToList())
            {
                Assignment assignment;
                if (observedWorkerKeys.Contains(workerKey) ||
                    !_assignments.TryGetValue(workerKey, out assignment))
                {
                    continue;
                }

                var age = observedTicks >= assignment.LastObservedTicks
                    ? observedTicks - assignment.LastObservedTicks
                    : long.MaxValue;
                if (age > retentionTicks)
                {
                    _assignments.Remove(workerKey);
                }
            }
        }

        public IEnumerable<string> ResourceKeys => _assignments.Values.Select(value => value.ResourceKey);
    }
}
