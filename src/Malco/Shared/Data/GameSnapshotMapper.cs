using System;
using System.Collections.Generic;
using System.Linq;
using Malco.Models;

namespace Malco.Data
{
    internal sealed class GameSnapshotMapper
    {
        private const int MutaliskUnitId = 43;
        private const int CocoonUnitId = 59;
        private readonly GasWorkerAssignmentProjector _gasWorkerProjector =
            new GasWorkerAssignmentProjector();
        private readonly MineralWorkerAssignmentProjector _mineralWorkerProjector =
            new MineralWorkerAssignmentProjector();
        private readonly UnitSpatialStateProjector _unitSpatialStateProjector =
            new UnitSpatialStateProjector();

        public void ResetSessionState()
        {
            _gasWorkerProjector.ResetSessionState();
            _mineralWorkerProjector.ResetSessionState();
            _unitSpatialStateProjector.ResetSessionState();
        }

        public GameSnapshot BuildSemanticSnapshot(
            BwrApiRuntimeSnapshot runtime,
            string workerStateStatus)
        {
            if (runtime == null)
            {
                return GameSnapshotFactory.NotReady("Waiting for in-process BWRAPI runtime");
            }

            var units = runtime.Units ?? new List<BwrApiRuntimeUnit>();
            var hasPerspectivePlayer = runtime.PerspectivePlayerId >= 0;
            var localUnits = hasPerspectivePlayer
                ? units.Where(unit => unit.OwnerId == runtime.PerspectivePlayerId).ToList()
                : new List<BwrApiRuntimeUnit>();
            var displayUnits = localUnits
                .Where(unit => !BwapiBroodWarTables.IsAuxiliarySubunit(unit.UnitId))
                .ToList();
            var countableUnits = displayUnits
                .Where(unit => !unit.IsHallucination)
                .ToList();
            var race = runtime.Race;
            var capturedAt = runtime.CapturedAt == default(DateTime) ? DateTime.Now : runtime.CapturedAt;
            // Production and cancellation can leave an incomplete worker CUnit
            // record in the runtime snapshot. Only completed workers contribute
            // to the HUD totals; order 0 alone is still not a death signal.
            var workers = countableUnits
                .Where(unit => unit.IsWorker && IsCompletedForPrerequisite(unit))
                .ToList();
            var workerTotalsByType = BwapiBroodWarTables.WorkerUnitTypeIds.ToDictionary(
                workerUnitId => workerUnitId,
                workerUnitId =>
                {
                    int authoritativeWorkerTotal;
                    return runtime.CompletedUnitCounts.TryGetValue(workerUnitId, out authoritativeWorkerTotal)
                        ? Math.Max(0, authoritativeWorkerTotal)
                        : workers.Count(unit => unit.UnitId == workerUnitId);
                });
            var workersTotal = workerTotalsByType.Values.Sum();
            var observedWorkersIdle = workers.Count(unit => WorkerOrderSemantics.IsIdle(unit.OrderId));
            var observedWorkersActive = workers.Count(unit =>
                IsWorkerActiveOrder(BwapiBroodWarTables.GetWorkerRace(unit.UnitId), unit.OrderId));
            // Harvesting workers can temporarily leave the public CUnit set.
            // The authoritative completed-unit table still counts them, and a
            // missing completed worker is active rather than idle.
            var workersIdle = Math.Min(workersTotal, observedWorkersIdle);
            var missingWorkers = Math.Max(0, workersTotal - workers.Count);
            var workersActive = Math.Min(
                workersTotal - workersIdle,
                observedWorkersActive + missingWorkers);
            var workersUnknown = workersTotal - workersIdle - workersActive;
            var unitCounts = BuildUnitCounts(countableUnits, false);
            foreach (var workerTotal in workerTotalsByType)
            {
                ApplyAuthoritativeWorkerCount(unitCounts, workerTotal.Key, workerTotal.Value);
            }
            ReconcileDisplayedMutaliskCount(
                unitCounts,
                countableUnits);
            var buildingCounts = BuildUnitCounts(countableUnits, true);
            HashSet<StableIdentity> gasWorkerKeys;
            var gasWorkerGroups = _gasWorkerProjector.Build(
                countableUnits,
                units,
                capturedAt,
                out gasWorkerKeys);
            var mineralWorkers = _mineralWorkerProjector.Build(
                countableUnits,
                units,
                capturedAt,
                gasWorkerKeys);
            var upgrades = BuildUpgrades(runtime.Upgrades);
            var availableUpgrades = BuildUpgrades(runtime.AvailableUpgrades);
            var unitSpatialStates = _unitSpatialStateProjector.Build(
                localUnits,
                runtime.CompletedUnitCounts);
            var ownedTechnologyRaces = OwnedTechnologyRacePolicy.Resolve(
                race,
                unitCounts,
                buildingCounts);
            upgrades = NormalizeUpgradeStatesForOwnedRaces(ownedTechnologyRaces, upgrades);
            var prerequisiteAvailableUpgrades = runtime.HasReliableUpgradeState
                ? ownedTechnologyRaces.SelectMany(raceValue =>
                    UpgradeAvailabilityPolicy.BuildPrerequisiteAvailableUpgradeStates(
                        raceValue,
                        buildingCounts,
                        upgrades))
                : new List<UpgradeState>();
            availableUpgrades = NormalizeAvailableUpgradeStatesForOwnedRaces(
                ownedTechnologyRaces,
                availableUpgrades.Concat(prerequisiteAvailableUpgrades),
                upgrades);
            return new GameSnapshot(
                capturedAt,
                runtime.IsInMatch,
                race,
                hasPerspectivePlayer ? runtime.PerspectivePlayerId : -1,
                workersTotal,
                workersIdle,
                workersActive,
                workersUnknown,
                workerStateStatus,
                unitCounts,
                buildingCounts,
                gasWorkerGroups,
                mineralWorkers,
                upgrades,
                availableUpgrades,
                unitSpatialStates);
        }

        private static List<UpgradeState> NormalizeUpgradeStatesForOwnedRaces(
            IEnumerable<Race> races,
            IEnumerable<UpgradeState> states)
        {
            var stateList = (states ?? Enumerable.Empty<UpgradeState>()).ToList();
            return DistinctUpgradeStates((races ?? Enumerable.Empty<Race>())
                .SelectMany(race => UpgradeAvailabilityPolicy.NormalizeUpgradeStatesForRace(race, stateList)));
        }

        private static List<UpgradeState> NormalizeAvailableUpgradeStatesForOwnedRaces(
            IEnumerable<Race> races,
            IEnumerable<UpgradeState> states,
            IEnumerable<UpgradeState> knownStates)
        {
            var stateList = (states ?? Enumerable.Empty<UpgradeState>()).ToList();
            var knownStateList = (knownStates ?? Enumerable.Empty<UpgradeState>()).ToList();
            return DistinctUpgradeStates((races ?? Enumerable.Empty<Race>())
                .SelectMany(race => UpgradeAvailabilityPolicy.NormalizeAvailableUpgradeStatesForRace(
                    race,
                    stateList,
                    knownStateList)));
        }

        private static List<UpgradeState> DistinctUpgradeStates(IEnumerable<UpgradeState> states)
        {
            return (states ?? Enumerable.Empty<UpgradeState>())
                .Where(state => state != null)
                .GroupBy(
                    state => state.StateKey ?? state.Name ?? string.Empty,
                    StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
        }

        private static List<UnitCount> BuildUnitCounts(IEnumerable<BwrApiRuntimeUnit> units, bool buildings)
        {
            var unitList = (units ?? Enumerable.Empty<BwrApiRuntimeUnit>()).ToList();
            var counts = unitList
                .Where(unit => unit.IsBuilding == buildings)
                .Where(unit => buildings || !IsProductionContainerUnit(unit.UnitId))
                .GroupBy(unit => new { unit.UnitId, unit.Name, unit.IconKey, unit.IsBuilding })
                .Select(group => new UnitCount
                {
                    UnitId = group.Key.UnitId,
                    Name = group.Key.Name,
                    IconKey = group.Key.IconKey,
                    IsBuilding = group.Key.IsBuilding,
                    Count = buildings ? group.Count() : group.Count(IsCompletedForPrerequisite),
                    CompletedCount = group.Count(IsCompletedForPrerequisite)
                })
                .ToList();

            return counts.OrderBy(unit => unit.Name).ToList();
        }

        private static bool IsCompletedForPrerequisite(BwrApiRuntimeUnit unit)
        {
            if (unit == null)
            {
                return false;
            }

            if (unit.IsCompleted)
            {
                return !RuntimeUnitSemantics.IsLiftedBuilding(unit);
            }

            return false;
        }

        private static bool IsProductionContainerUnit(int unitTypeId)
        {
            return unitTypeId == 36 ||
                   unitTypeId == 97;
        }

        private static List<UpgradeState> BuildUpgrades(IEnumerable<BwrApiRuntimeUpgrade> upgrades)
        {
            return (upgrades ?? Enumerable.Empty<BwrApiRuntimeUpgrade>())
                .Select(upgrade => new UpgradeState
                {
                    StateKey = upgrade.StateKey,
                    Name = upgrade.Name,
                    Level = upgrade.Level,
                    ProgressPercent = upgrade.ProgressPercent,
                    SecondsRemaining = upgrade.SecondsRemaining,
                    SecondsRemainingPrecise = upgrade.SecondsRemainingPrecise,
                    IsComplete = upgrade.IsComplete,
                    IsInProgress = upgrade.IsInProgress,
                    IsAvailable = upgrade.IsAvailable,
                    IsBlocked = false
                })
                .ToList();
        }

        private static bool IsWorkerActiveOrder(Race race, int order)
        {
            return WorkerOrderSemantics.IsActive(race, order);
        }

        private static void ApplyAuthoritativeWorkerCount(
            List<UnitCount> counts,
            int workerUnitId,
            int workersTotal)
        {
            if (workerUnitId < 0 || counts == null)
            {
                return;
            }
            var workerIndex = counts.FindIndex(unit => unit.UnitId == workerUnitId);
            var worker = workerIndex >= 0 ? counts[workerIndex] : null;
            if (worker == null && workersTotal > 0)
            {
                var type = BwapiBroodWarTables.GetUnitTypeInfo(workerUnitId);
                worker = new UnitCount
                {
                    UnitId = workerUnitId,
                    Name = type.Name,
                    IconKey = type.IconKey,
                    IsBuilding = false
                };
                counts.Add(worker);
                workerIndex = counts.Count - 1;
            }
            if (worker != null)
            {
                counts[workerIndex] = new UnitCount
                {
                    UnitId = worker.UnitId,
                    Name = worker.Name,
                    IconKey = worker.IconKey,
                    IsBuilding = worker.IsBuilding,
                    Count = workersTotal,
                    CompletedCount = workersTotal
                };
            }
        }

        private static void ReconcileDisplayedMutaliskCount(
            List<UnitCount> counts,
            IEnumerable<BwrApiRuntimeUnit> units)
        {
            if (counts == null)
            {
                return;
            }

            counts.RemoveAll(unit => unit != null && unit.UnitId == CocoonUnitId);
            var displayedCount = (units ?? Enumerable.Empty<BwrApiRuntimeUnit>())
                .Count(unit => unit != null &&
                               (unit.UnitId == MutaliskUnitId ||
                                unit.UnitId == CocoonUnitId));
            var mutaliskIndex = counts.FindIndex(unit => unit != null && unit.UnitId == MutaliskUnitId);
            var mutalisk = mutaliskIndex >= 0 ? counts[mutaliskIndex] : null;

            if (displayedCount == 0)
            {
                if (mutaliskIndex >= 0)
                {
                    counts.RemoveAt(mutaliskIndex);
                }
                return;
            }

            if (mutalisk == null)
            {
                var type = BwapiBroodWarTables.GetUnitTypeInfo(MutaliskUnitId);
                counts.Add(new UnitCount
                {
                    UnitId = MutaliskUnitId,
                    Name = type.Name,
                    IconKey = type.IconKey,
                    IsBuilding = false,
                    Count = displayedCount,
                    CompletedCount = 0
                });
                return;
            }

            counts[mutaliskIndex] = new UnitCount
            {
                UnitId = mutalisk.UnitId,
                Name = mutalisk.Name,
                IconKey = mutalisk.IconKey,
                IsBuilding = mutalisk.IsBuilding,
                Count = displayedCount,
                CompletedCount = mutalisk.CompletedCount
            };
        }

    }
}
