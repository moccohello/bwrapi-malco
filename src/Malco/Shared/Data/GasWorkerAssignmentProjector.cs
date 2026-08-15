using System;
using System.Collections.Generic;
using System.Linq;
using Malco.Models;

namespace Malco.Data
{
    internal sealed class GasWorkerAssignmentProjector
    {
        private static readonly long AssignmentRetentionTicks =
            TimeSpan.FromSeconds(30d).Ticks;
        private readonly WorkerResourceAssignmentTracker<StableIdentity> _assignments =
            new WorkerResourceAssignmentTracker<StableIdentity>();

        public List<GasWorkerGroup> Build(
            IList<BwrApiRuntimeUnit> localUnits,
            IList<BwrApiRuntimeUnit> allUnits,
            DateTime capturedAt,
            out HashSet<StableIdentity> assignedWorkerKeys)
        {
            var gasBuildingUnits = localUnits
                .Where(unit =>
                    unit.IsCompleted &&
                    (unit.UnitId == 110 ||
                     unit.UnitId == 149 ||
                     unit.UnitId == 157))
                .ToList();
            var gasBuildings = gasBuildingUnits
                .Select(RuntimeUnitCoordinates.Resolve)
                .Where(WorkerResourceGeometry.IsNonZeroPosition)
                .ToList();
            var mineralFields = (allUnits ?? new BwrApiRuntimeUnit[0])
                .Where(WorkerResourceSemantics.IsLiveMineralField)
                .Select(RuntimeUnitCoordinates.Resolve)
                .Where(WorkerResourceGeometry.IsNonZeroPosition)
                .ToList();
            if (gasBuildings.Count == 0)
            {
                _assignments.Clear();
                assignedWorkerKeys = new HashSet<StableIdentity>();
                return new List<GasWorkerGroup>();
            }

            var groups =
                new Dictionary<string, GasWorkerGroup>(StringComparer.Ordinal);
            var workerCounts =
                new Dictionary<string, int>(StringComparer.Ordinal);
            var gasBuildingKeysByTag = gasBuildingUnits
                .Where(unit => !string.IsNullOrWhiteSpace(unit.UnitTag))
                .Select(unit => new
                {
                    unit.UnitTag,
                    Position = RuntimeUnitCoordinates.Resolve(unit)
                })
                .Where(item =>
                    WorkerResourceGeometry.IsNonZeroPosition(item.Position))
                .GroupBy(item => item.UnitTag, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => WorkerResourceGeometry.BuildPositionKey(
                        group.First().Position),
                    StringComparer.Ordinal);
            foreach (var gasBuildingUnit in gasBuildingUnits)
            {
                var building = RuntimeUnitCoordinates.Resolve(gasBuildingUnit);
                if (!WorkerResourceGeometry.IsNonZeroPosition(building))
                {
                    continue;
                }
                var key = WorkerResourceGeometry.BuildPositionKey(building);
                groups[key] = new GasWorkerGroup
                {
                    GasIdentity = StableIdentity.Create(
                        "gas",
                        !string.IsNullOrWhiteSpace(gasBuildingUnit.UnitTag)
                            ? gasBuildingUnit.UnitTag
                            : "position:" + key),
                    UnitId = gasBuildingUnit.UnitId,
                    MapX = building.Item1,
                    MapY = building.Item2,
                    WorkerCount = 0
                };
                workerCounts[key] = 0;
            }

            var observedWorkerKeys = new HashSet<StableIdentity>();
            foreach (var worker in
                     localUnits.Where(unit => unit != null && unit.IsWorker))
            {
                observedWorkerKeys.Add(worker.SourceIdentity);
                string trackedKey;
                var hasTrackedAssignment = _assignments.TryGet(
                    worker.SourceIdentity,
                    out trackedKey);
                string key = null;
                var hasTaggedGasAssignment =
                    !string.IsNullOrWhiteSpace(worker.GasResourceUnitTag) &&
                    gasBuildingKeysByTag.TryGetValue(
                        worker.GasResourceUnitTag,
                        out key);
                if (!hasTaggedGasAssignment)
                {
                    if (WorkerOrderSemantics.IsGas(worker.OrderId))
                    {
                        var position =
                            WorkerResourceTargetResolver.ResolveWorkerPosition(
                            worker,
                            gasBuildings,
                            320L * 320L);
                        var anchored = WorkerResourceGeometry.SnapToNearest(
                            position,
                            gasBuildings,
                            320L * 320L);
                        key = WorkerResourceGeometry.IsNonZeroPosition(anchored)
                            ? WorkerResourceGeometry.BuildPositionKey(anchored)
                            : null;
                    }
                    else if (WorkerOrderSemantics.IsGenericHarvest(
                                 worker.OrderId))
                    {
                        key =
                            WorkerResourceTargetResolver.ResolveGenericHarvestGasKey(
                                worker,
                                gasBuildings,
                                mineralFields);
                    }
                    else
                    {
                        if (WorkerResourceSemantics.CanRetainTransition(
                            worker,
                            hasTrackedAssignment))
                        {
                            continue;
                        }
                        _assignments.Remove(worker.SourceIdentity);
                        continue;
                    }
                }
                var canReuseTrackedAssignment =
                    WorkerOrderSemantics.IsGas(worker.OrderId) ||
                    (WorkerOrderSemantics.IsGenericHarvest(worker.OrderId) &&
                     !WorkerResourceSemantics.HasResourceTarget(worker));
                if (string.IsNullOrEmpty(key) &&
                    hasTrackedAssignment &&
                    canReuseTrackedAssignment)
                {
                    key = trackedKey;
                }

                if (string.IsNullOrEmpty(key))
                {
                    _assignments.Remove(worker.SourceIdentity);
                    continue;
                }

                GasWorkerGroup group;
                if (!groups.TryGetValue(key, out group))
                {
                    continue;
                }

                _assignments.Set(
                    worker.SourceIdentity,
                    key,
                    capturedAt.Ticks);
            }

            // CUnit +0x128 identifies the refinery even on ReturnGas. Workers
            // can still disappear from the released snapshot while inside the
            // building, so retain that factual link across collection cycles.
            // A visible non-gas order removes it immediately; the timeout only
            // bounds a missing record that never reappears.
            _assignments.PruneMissing(
                observedWorkerKeys,
                capturedAt.Ticks,
                AssignmentRetentionTicks);
            foreach (var key in _assignments.ResourceKeys)
            {
                if (!string.IsNullOrEmpty(key) &&
                    workerCounts.ContainsKey(key))
                {
                    workerCounts[key] = workerCounts[key] + 1;
                }
            }

            var result = groups
                .Select(pair => new GasWorkerGroup
                {
                    GasIdentity = pair.Value.GasIdentity,
                    UnitId = pair.Value.UnitId,
                    MapX = pair.Value.MapX,
                    MapY = pair.Value.MapY,
                    WorkerCount = workerCounts[pair.Key]
                })
                .OrderBy(group => group.GasIdentity)
                .ToList();
            assignedWorkerKeys = new HashSet<StableIdentity>(
                _assignments.SnapshotWorkerKeys());
            return result;
        }

        public void ResetSessionState()
        {
            _assignments.Clear();
        }

    }
}
