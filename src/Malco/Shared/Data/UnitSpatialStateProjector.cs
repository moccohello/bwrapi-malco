using System;
using System.Collections.Generic;
using System.Linq;
using Malco.Models;

namespace Malco.Data
{
    internal sealed class UnitSpatialStateProjector
    {
        private readonly Dictionary<string, CargoAssignment> _cargoAssignments =
            new Dictionary<string, CargoAssignment>(StringComparer.Ordinal);

        public void ResetSessionState()
        {
            _cargoAssignments.Clear();
        }

        public List<UnitSpatialState> Build(
            IReadOnlyCollection<BwrApiRuntimeUnit> localUnits,
            IReadOnlyDictionary<int, int> completedUnitCounts)
        {
            var units = localUnits ?? Array.Empty<BwrApiRuntimeUnit>();
            var liveTransportTags = units
                .Where(unit =>
                    unit != null &&
                    unit.HitPointsRaw > 0 &&
                    IsTransportUnit(unit.UnitId) &&
                    !string.IsNullOrWhiteSpace(unit.UnitTag))
                .Select(unit => unit.UnitTag)
                .ToHashSet(StringComparer.Ordinal);
            foreach (var unit in units.Where(unit =>
                         unit != null &&
                         !string.IsNullOrWhiteSpace(unit.UnitTag)))
            {
                if (unit.HitPointsRaw <= 0 ||
                    unit.IsHallucination ||
                    unit.IsLoaded == false)
                {
                    _cargoAssignments.Remove(unit.UnitTag);
                    continue;
                }

                if (unit.IsLoaded == true &&
                    !string.IsNullOrWhiteSpace(unit.TransportUnitTag) &&
                    liveTransportTags.Contains(unit.TransportUnitTag))
                {
                    _cargoAssignments[unit.UnitTag] = new CargoAssignment
                    {
                        PassengerUnitTag = unit.UnitTag,
                        TransportUnitTag = unit.TransportUnitTag,
                        UnitId = unit.UnitId,
                        Name = unit.Name,
                        IconKey = unit.IconKey
                    };
                }
            }

            foreach (var passengerTag in _cargoAssignments
                         .Where(pair =>
                             !liveTransportTags.Contains(pair.Value.TransportUnitTag))
                         .Select(pair => pair.Key)
                         .ToList())
            {
                _cargoAssignments.Remove(passengerTag);
            }

            ReconcileCargoAssignments(units, completedUnitCounts);

            var cargoByTransport = _cargoAssignments.Values
                .GroupBy(unit => unit.TransportUnitTag, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .GroupBy(unit => new { unit.UnitId, unit.Name, unit.IconKey })
                        .Select(cargo => new CargoUnitCount
                        {
                            UnitId = cargo.Key.UnitId,
                            Name = cargo.Key.Name,
                            IconKey = cargo.Key.IconKey,
                            Count = cargo.Count()
                        })
                        .OrderBy(cargo => cargo.UnitId)
                        .ToList(),
                    StringComparer.Ordinal);

            var result = new List<UnitSpatialState>();
            foreach (var unit in units.Where(unit =>
                         !unit.IsHallucination &&
                         string.IsNullOrWhiteSpace(unit.TransportUnitTag)))
            {
                List<CargoUnitCount> cargo = null;
                if (IsTransportUnit(unit.UnitId))
                {
                    cargoByTransport.TryGetValue(
                        unit.UnitTag ?? string.Empty,
                        out cargo);
                }

                var spellcaster = AbilityCatalog.Find(unit.UnitId) != null;
                if (!spellcaster && (cargo == null || cargo.Count == 0))
                {
                    continue;
                }

                Tuple<int, int> position = RuntimeUnitCoordinates.Resolve(unit);
                result.Add(new UnitSpatialState
                {
                    UnitTag = unit.UnitTag ?? string.Empty,
                    UnitId = unit.UnitId,
                    Name = unit.Name,
                    IconKey = unit.IconKey,
                    MapX = position.Item1,
                    MapY = position.Item2,
                    Energy = unit.EnergyRaw.HasValue
                        ? unit.EnergyRaw.Value / 256
                        : (int?)null,
                    Cargo = cargo ?? new List<CargoUnitCount>()
                });
            }

            return result;
        }

        private void ReconcileCargoAssignments(
            IEnumerable<BwrApiRuntimeUnit> units,
            IReadOnlyDictionary<int, int> completedUnitCounts)
        {
            if (completedUnitCounts == null || completedUnitCounts.Count == 0)
            {
                return;
            }

            var observedUnitTags = (units ?? Enumerable.Empty<BwrApiRuntimeUnit>())
                .Where(unit =>
                    unit != null &&
                    unit.HitPointsRaw > 0 &&
                    unit.IsCompleted &&
                    !unit.IsHallucination &&
                    !string.IsNullOrWhiteSpace(unit.UnitTag))
                .Select(unit => unit.UnitTag)
                .ToHashSet(StringComparer.Ordinal);
            var observedCounts = (units ?? Enumerable.Empty<BwrApiRuntimeUnit>())
                .Where(unit =>
                    unit != null &&
                    unit.HitPointsRaw > 0 &&
                    unit.IsCompleted &&
                    !unit.IsHallucination &&
                    !string.IsNullOrWhiteSpace(unit.UnitTag))
                .GroupBy(unit => unit.UnitId)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(unit => unit.UnitTag).Distinct().Count());
            foreach (var assignmentsByType in _cargoAssignments.Values
                         .Where(assignment =>
                             !observedUnitTags.Contains(assignment.PassengerUnitTag))
                         .GroupBy(assignment => assignment.UnitId)
                         .ToList())
            {
                int authoritativeTotal;
                if (!completedUnitCounts.TryGetValue(
                        assignmentsByType.Key,
                        out authoritativeTotal))
                {
                    continue;
                }

                int observedCount;
                observedCounts.TryGetValue(
                    assignmentsByType.Key,
                    out observedCount);
                var excess =
                    observedCount +
                    assignmentsByType.Count() -
                    Math.Max(0, authoritativeTotal);
                foreach (var assignment in assignmentsByType
                             .OrderBy(
                                 item => item.PassengerUnitTag,
                                 StringComparer.Ordinal)
                             .Take(Math.Max(0, excess)))
                {
                    _cargoAssignments.Remove(assignment.PassengerUnitTag);
                }
            }
        }

        private static bool IsTransportUnit(int unitId)
        {
            return unitId == 11 || unitId == 42 || unitId == 69;
        }

        private sealed class CargoAssignment
        {
            public string PassengerUnitTag { get; set; }
            public string TransportUnitTag { get; set; }
            public int UnitId { get; set; }
            public string Name { get; set; }
            public string IconKey { get; set; }
        }
    }
}
