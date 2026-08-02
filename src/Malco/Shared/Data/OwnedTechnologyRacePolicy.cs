using System;
using System.Collections.Generic;
using System.Linq;
using Malco.Models;

namespace Malco.Data
{
    internal static class OwnedTechnologyRacePolicy
    {
        internal static IReadOnlyList<Race> Resolve(
            Race primaryRace,
            IEnumerable<UnitCount> unitCounts,
            IEnumerable<UnitCount> buildingCounts)
        {
            var races = new List<Race>();
            AddRace(races, primaryRace);

            foreach (var unit in unitCounts ?? Enumerable.Empty<UnitCount>())
            {
                if (unit != null && unit.Count > 0)
                {
                    AddRace(races, BwapiBroodWarTables.GetWorkerRace(unit.UnitId));
                }
            }

            foreach (var building in buildingCounts ?? Enumerable.Empty<UnitCount>())
            {
                if (building != null && building.Count > 0)
                {
                    AddRace(races, BuildingRace(building.UnitId));
                }
            }

            if (races.Count == 0)
            {
                races.Add(Race.Unknown);
            }
            return races;
        }

        private static Race BuildingRace(int unitId)
        {
            if (unitId >= 106 && unitId <= 129)
            {
                return Race.Terran;
            }
            if (unitId >= 130 && unitId <= 153)
            {
                return Race.Zerg;
            }
            if (unitId >= 154 && unitId <= 175)
            {
                return Race.Protoss;
            }
            return Race.Unknown;
        }

        private static void AddRace(ICollection<Race> races, Race race)
        {
            if ((race == Race.Terran || race == Race.Zerg || race == Race.Protoss) &&
                !races.Contains(race))
            {
                races.Add(race);
            }
        }
    }
}
