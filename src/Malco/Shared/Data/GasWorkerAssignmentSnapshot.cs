using System.Collections.Generic;
using Malco.Models;

namespace Malco.Data
{
    internal sealed class GasWorkerAssignmentSnapshot
    {
        private readonly HashSet<StableIdentity> _workerKeys;

        public GasWorkerAssignmentSnapshot(IEnumerable<StableIdentity> workerKeys)
        {
            _workerKeys = new HashSet<StableIdentity>(
                workerKeys ?? new StableIdentity[0]);
        }

        public bool ContainsWorker(StableIdentity workerKey)
        {
            return _workerKeys.Contains(workerKey);
        }
    }
}
