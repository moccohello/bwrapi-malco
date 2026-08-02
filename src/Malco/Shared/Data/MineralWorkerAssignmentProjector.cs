using System;
using System.Collections.Generic;
using System.Linq;
using Malco.Models;

namespace Malco.Data
{
    internal sealed class MineralWorkerAssignmentProjector
    {
        private static readonly long AssignmentRetentionTicks =
            TimeSpan.FromMilliseconds(250d).Ticks;
        private readonly WorkerResourceAssignmentTracker<StableIdentity> _assignments =
            new WorkerResourceAssignmentTracker<StableIdentity>();

        public List<MineralWorkerGroup> Build(
            IList<BwrApiRuntimeUnit> localUnits,
            IList<BwrApiRuntimeUnit> allUnits,
            DateTime capturedAt,
            GasWorkerAssignmentSnapshot gasAssignments)
        {
            var mineralFields = allUnits
                .Where(WorkerResourceSemantics.IsLiveMineralField)
                .Select(RuntimeUnitCoordinates.Resolve)
                .Where(WorkerResourceGeometry.IsNonZeroPosition)
                .ToList();
            var bases = localUnits
                .Where(unit =>
                    (unit.UnitId == 106 ||
                     unit.UnitId == 131 ||
                     unit.UnitId == 132 ||
                     unit.UnitId == 133 ||
                     unit.UnitId == 154) &&
                    !RuntimeUnitSemantics.IsLiftedBuilding(unit))
                .Select((unit, index) =>
                {
                    Tuple<int, int> position =
                        RuntimeUnitCoordinates.Resolve(unit);
                    var sourceIdentity = unit.SourceIdentity.IsEmpty
                        ? StableIdentity.Create(
                            "base",
                            "position:" + WorkerResourceGeometry.BuildPositionKey(position))
                        : StableIdentity.Create("base", unit.SourceIdentity.Value);
                    return new MineralBaseCandidate
                    {
                        Key = sourceIdentity.Value,
                        ResourceIdentity = sourceIdentity,
                        UnitId = unit.UnitId,
                        MapX = position.Item1,
                        MapY = position.Item2,
                        Order = index
                    };
                })
                .Where(candidate =>
                    WorkerResourceGeometry.IsNonZeroPosition(
                        Tuple.Create(candidate.MapX, candidate.MapY)))
                .ToList();
            var mineralClusters =
                WorkerResourceGeometry.BuildMineralClusters(mineralFields);
            var primaryBases = WorkerResourceGeometry.SelectMineralClusterBases(
                bases,
                mineralClusters);
            if (primaryBases.Count == 0)
            {
                _assignments.Clear();
                return new List<MineralWorkerGroup>();
            }

            var workerCountsByBase =
                new Dictionary<string, int>(StringComparer.Ordinal);
            var basesByKey = primaryBases.ToDictionary(
                candidate => candidate.Key,
                StringComparer.Ordinal);
            var observedWorkerKeys = new HashSet<StableIdentity>();
            foreach (var worker in
                     localUnits.Where(unit => unit != null && unit.IsWorker))
            {
                observedWorkerKeys.Add(worker.SourceIdentity);
                var isTrackedGasTransition =
                    WorkerOrderSemantics.IsGenericHarvest(worker.OrderId) &&
                    gasAssignments.ContainsWorker(worker.SourceIdentity);
                if (WorkerOrderSemantics.IsGas(worker.OrderId) ||
                    isTrackedGasTransition ||
                    !WorkerOrderSemantics.IsMineral(worker.OrderId))
                {
                    string transitionPreviousKey;
                    var hasTransitionPrevious = _assignments.TryGet(
                        worker.SourceIdentity,
                        out transitionPreviousKey);
                    if (!WorkerOrderSemantics.IsGas(worker.OrderId) &&
                        !isTrackedGasTransition &&
                        WorkerResourceSemantics.CanRetainTransition(
                            worker,
                            hasTransitionPrevious))
                    {
                        continue;
                    }
                    _assignments.Remove(worker.SourceIdentity);
                    continue;
                }

                var baseCandidate =
                    WorkerResourceTargetResolver.ResolveMineralBase(
                        worker,
                        mineralFields,
                        primaryBases);
                string previousKey;
                var hasPrevious = _assignments.TryGet(
                    worker.SourceIdentity,
                    out previousKey);
                var key =
                    worker.OrderId == 90 &&
                    hasPrevious &&
                    basesByKey.ContainsKey(previousKey)
                        ? previousKey
                        : baseCandidate?.Key;
                if (string.IsNullOrEmpty(key) &&
                    hasPrevious &&
                    basesByKey.ContainsKey(previousKey))
                {
                    key = previousKey;
                }
                if (string.IsNullOrEmpty(key) ||
                    !basesByKey.ContainsKey(key))
                {
                    continue;
                }

                _assignments.Set(
                    worker.SourceIdentity,
                    key,
                    capturedAt.Ticks);
            }
            _assignments.PruneMissing(
                observedWorkerKeys,
                capturedAt.Ticks,
                AssignmentRetentionTicks);
            foreach (var key in _assignments.ResourceKeys)
            {
                if (string.IsNullOrEmpty(key) ||
                    !basesByKey.ContainsKey(key))
                {
                    continue;
                }
                int count;
                workerCountsByBase.TryGetValue(key, out count);
                workerCountsByBase[key] = count + 1;
            }

            return primaryBases
                .Select(baseCandidate =>
                {
                    int workerCount;
                    workerCountsByBase.TryGetValue(
                        baseCandidate.Key,
                        out workerCount);
                    return new MineralWorkerGroup
                    {
                        BaseIdentity = baseCandidate.ResourceIdentity,
                        UnitId = baseCandidate.UnitId,
                        MapX = baseCandidate.MapX,
                        MapY = baseCandidate.MapY,
                        WorkerCount = workerCount,
                        MineralPatchCount = baseCandidate.MineralPatchCount
                    };
                })
                .ToList();
        }

        public void ResetSessionState()
        {
            _assignments.Clear();
        }
    }
}
