using System.Collections.Generic;
using Malco.Models;

namespace Malco.Data
{
    internal sealed class WorkerResourceGroupProjection
    {
        public WorkerResourceGroupProjection(
            List<GasWorkerGroup> gasWorkerGroups,
            List<MineralWorkerGroup> mineralWorkerGroups)
        {
            GasWorkerGroups = gasWorkerGroups;
            MineralWorkerGroups = mineralWorkerGroups;
        }

        public List<GasWorkerGroup> GasWorkerGroups { get; }

        public List<MineralWorkerGroup> MineralWorkerGroups { get; }
    }
}
