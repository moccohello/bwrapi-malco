using System;
using System.Collections.Generic;

namespace Malco.Data
{
    internal sealed class WorkerResourceGroupProjector
    {
        private readonly GasWorkerAssignmentProjector _gas =
            new GasWorkerAssignmentProjector();
        private readonly MineralWorkerAssignmentProjector _minerals =
            new MineralWorkerAssignmentProjector();

        public WorkerResourceGroupProjection Build(
            IList<BwrApiRuntimeUnit> countableLocalUnits,
            IList<BwrApiRuntimeUnit> allUnits,
            DateTime capturedAt)
        {
            var gas = _gas.Build(
                countableLocalUnits,
                allUnits,
                capturedAt);
            var minerals = _minerals.Build(
                countableLocalUnits,
                allUnits,
                capturedAt,
                gas.Assignments);
            return new WorkerResourceGroupProjection(
                gas.Groups,
                minerals);
        }

        public void ResetSessionState()
        {
            _gas.ResetSessionState();
            _minerals.ResetSessionState();
        }
    }
}
