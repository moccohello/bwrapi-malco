using System;
using System.Collections.Generic;
using System.Linq;

namespace Malco.Data
{
    internal static class WorkerResourceTargetResolver
    {
        private const long BaseWorkerRadiusSquared = 512L * 512L;

        public static Tuple<int, int> ResolveWorkerPosition(
            BwrApiRuntimeUnit worker,
            IList<Tuple<int, int>> resourcePositions,
            long maxDistanceSquared)
        {
            if (worker == null)
            {
                return null;
            }

            var target = ResolveOrderTargetOrCurrentPosition(worker);
            var anchored = WorkerResourceGeometry.SnapToNearest(
                target,
                resourcePositions,
                maxDistanceSquared);
            if (IsExactResourcePosition(anchored, resourcePositions))
            {
                return anchored;
            }

            var current = RuntimeUnitCoordinates.Resolve(worker);
            anchored = WorkerResourceGeometry.SnapToNearest(
                current,
                resourcePositions,
                maxDistanceSquared);
            return IsExactResourcePosition(anchored, resourcePositions)
                ? anchored
                : null;
        }

        public static MineralBaseCandidate ResolveMineralBase(
            BwrApiRuntimeUnit worker,
            IList<Tuple<int, int>> mineralFields,
            IList<MineralBaseCandidate> primaryBases)
        {
            if (worker == null)
            {
                return null;
            }

            var target = ResolveOrderTargetOrCurrentPosition(worker);
            var fieldPosition = WorkerResourceGeometry.SnapToNearest(
                target,
                mineralFields,
                224L * 224L);
            var baseCandidate = FindNearestMineralBase(
                fieldPosition,
                primaryBases,
                BaseWorkerRadiusSquared);
            if (baseCandidate != null)
            {
                return baseCandidate;
            }

            var current = RuntimeUnitCoordinates.Resolve(worker);
            fieldPosition = WorkerResourceGeometry.SnapToNearest(
                current,
                mineralFields,
                320L * 320L);
            baseCandidate = FindNearestMineralBase(
                fieldPosition,
                primaryBases,
                BaseWorkerRadiusSquared);
            if (baseCandidate != null)
            {
                return baseCandidate;
            }

            return FindNearestMineralBase(
                current,
                primaryBases,
                BaseWorkerRadiusSquared);
        }

        public static string ResolveGenericHarvestGasKey(
            BwrApiRuntimeUnit worker,
            IList<Tuple<int, int>> gasBuildings,
            IList<Tuple<int, int>> mineralFields)
        {
            if (!WorkerResourceSemantics.HasResourceTarget(worker))
            {
                return null;
            }

            var target = Tuple.Create(
                worker.OrderTargetMapX,
                worker.OrderTargetMapY);
            var nearestGas = WorkerResourceGeometry.NearestDistanceSquared(
                target,
                gasBuildings);
            var nearestMineral = WorkerResourceGeometry.NearestDistanceSquared(
                target,
                mineralFields);
            if (nearestGas > 320L * 320L || nearestMineral < nearestGas)
            {
                return null;
            }

            var anchored = WorkerResourceGeometry.SnapToNearest(
                target,
                gasBuildings,
                320L * 320L);
            return WorkerResourceGeometry.IsNonZeroPosition(anchored)
                ? WorkerResourceGeometry.BuildPositionKey(anchored)
                : null;
        }

        private static MineralBaseCandidate FindNearestMineralBase(
            Tuple<int, int> position,
            IList<MineralBaseCandidate> bases,
            long maxDistanceSquared)
        {
            if (!WorkerResourceGeometry.IsNonZeroPosition(position))
            {
                return null;
            }

            MineralBaseCandidate nearest = null;
            var nearestDistance = long.MaxValue;
            foreach (var baseCandidate in
                     (bases ?? new MineralBaseCandidate[0])
                     .Where(candidate => candidate != null))
            {
                var distance = WorkerResourceGeometry.DistanceSquared(
                    position.Item1,
                    position.Item2,
                    baseCandidate.MapX,
                    baseCandidate.MapY);
                if (distance < nearestDistance)
                {
                    nearest = baseCandidate;
                    nearestDistance = distance;
                }
            }

            return nearest != null && nearestDistance <= maxDistanceSquared
                ? nearest
                : null;
        }

        private static Tuple<int, int> ResolveOrderTargetOrCurrentPosition(
            BwrApiRuntimeUnit unit)
        {
            return unit.HasOrderTarget
                ? Tuple.Create(unit.OrderTargetMapX, unit.OrderTargetMapY)
                : RuntimeUnitCoordinates.Resolve(unit);
        }

        private static bool IsExactResourcePosition(
            Tuple<int, int> position,
            IList<Tuple<int, int>> resourcePositions)
        {
            return WorkerResourceGeometry.IsNonZeroPosition(position) &&
                   resourcePositions != null &&
                   resourcePositions.Any(resource =>
                       WorkerResourceGeometry.IsNonZeroPosition(resource) &&
                       resource.Item1 == position.Item1 &&
                       resource.Item2 == position.Item2);
        }
    }
}
