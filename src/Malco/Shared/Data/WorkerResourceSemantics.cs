using System;

namespace Malco.Data
{
    internal static class WorkerResourceSemantics
    {
        public static bool IsLiveMineralField(BwrApiRuntimeUnit unit)
        {
            return unit != null &&
                   (unit.UnitId == 176 ||
                    unit.UnitId == 177 ||
                    unit.UnitId == 178) &&
                   unit.HitPointsRaw > 0 &&
                   unit.ResourceAmount.HasValue &&
                   unit.ResourceAmount.Value > 0;
        }

        public static bool HasResourceTarget(BwrApiRuntimeUnit worker)
        {
            return worker != null &&
                   worker.HasOrderTarget &&
                   WorkerResourceGeometry.IsNonZeroPosition(Tuple.Create(
                       worker.OrderTargetMapX,
                       worker.OrderTargetMapY));
        }

        public static bool CanRetainTransition(
            BwrApiRuntimeUnit worker,
            bool hasAssignment)
        {
            return hasAssignment &&
                   worker != null &&
                   WorkerOrderSemantics.IsResourceTransition(worker.OrderId);
        }
    }
}
