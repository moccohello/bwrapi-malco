using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Malco.Models;

namespace Malco.Data
{
    // Product-owned display policy derived from typed runtime facts.
    internal static partial class UpgradeAvailabilityPolicy
    {
        private static readonly HashSet<int> TerranThreeLevelUpgradeIds = new HashSet<int> { 0, 1, 2, 7, 8, 9 };
        private static readonly HashSet<int> ZergHatcheryTierUpgradeIds = new HashSet<int> { 3, 4, 10, 11, 12 };
        private static readonly HashSet<int> ProtossGroundThreeLevelUpgradeIds = new HashSet<int> { 5, 13, 15 };
        private static readonly HashSet<int> ProtossAirThreeLevelUpgradeIds = new HashSet<int> { 6, 14 };
        private static readonly HashSet<int> NonResearchTechTypeIds = new HashSet<int>
        {
            4,  // Scanner Sweep
            6,  // Defensive Matrix
            12, // Infestation
            14, // Dark Swarm
            18, // Parasite
            23, // Archon Warp
            28, // Dark Archon Meld
            29, // Feedback
            34, // Healing
            44, // None
            45  // Nuclear Strike
        };

        private static readonly UpgradeOpportunityRule[] UpgradeOpportunityRules =
        {
            new UpgradeOpportunityRule(true, 0, Race.Terran, 122),
            new UpgradeOpportunityRule(true, 7, Race.Terran, 122),
            new UpgradeOpportunityRule(true, 1, Race.Terran, 123),
            new UpgradeOpportunityRule(true, 2, Race.Terran, 123),
            new UpgradeOpportunityRule(true, 8, Race.Terran, 123),
            new UpgradeOpportunityRule(true, 9, Race.Terran, 123),
            new UpgradeOpportunityRule(true, 16, Race.Terran, 112),
            new UpgradeOpportunityRule(true, 17, Race.Terran, new[] { 113 }, new[] { 120 }),
            new UpgradeOpportunityRule(true, 19, Race.Terran, new[] { 116 }, new[] { 118 }),
            new UpgradeOpportunityRule(true, 20, Race.Terran, new[] { 116 }, new[] { 117 }),
            new UpgradeOpportunityRule(true, 21, Race.Terran, new[] { 116 }, new[] { 117 }),
            new UpgradeOpportunityRule(true, 22, Race.Terran, new[] { 114 }, new[] { 115 }),
            new UpgradeOpportunityRule(true, 23, Race.Terran, new[] { 116 }, new[] { 118 }),
            new UpgradeOpportunityRule(true, 51, Race.Terran, 112),
            new UpgradeOpportunityRule(true, 54, Race.Terran, new[] { 113 }, new[] { 120 }, new[] { 123 }),
            new UpgradeOpportunityRule(false, 0, Race.Terran, 112),
            new UpgradeOpportunityRule(false, 1, Race.Terran, new[] { 116 }, new[] { 117 }),
            new UpgradeOpportunityRule(false, 2, Race.Terran, 116),
            new UpgradeOpportunityRule(false, 3, Race.Terran, new[] { 113 }, new[] { 120 }),
            new UpgradeOpportunityRule(false, 5, Race.Terran, new[] { 113 }, new[] { 120 }),
            new UpgradeOpportunityRule(false, 7, Race.Terran, 116),
            new UpgradeOpportunityRule(false, 8, Race.Terran, new[] { 116 }, new[] { 118 }),
            new UpgradeOpportunityRule(false, 9, Race.Terran, new[] { 114 }, new[] { 115 }),
            new UpgradeOpportunityRule(false, 10, Race.Terran, new[] { 116 }, new[] { 117 }),
            new UpgradeOpportunityRule(false, 24, Race.Terran, 112),
            new UpgradeOpportunityRule(false, 30, Race.Terran, 112),
            new UpgradeOpportunityRule(true, 3, Race.Zerg, 139),
            new UpgradeOpportunityRule(true, 10, Race.Zerg, 139),
            new UpgradeOpportunityRule(true, 11, Race.Zerg, 139),
            new UpgradeOpportunityRule(true, 4, Race.Zerg, 141, 137),
            new UpgradeOpportunityRule(true, 12, Race.Zerg, 141, 137),
            new UpgradeOpportunityRule(true, 24, Race.Zerg, 132, 133),
            new UpgradeOpportunityRule(true, 25, Race.Zerg, 132, 133),
            new UpgradeOpportunityRule(true, 26, Race.Zerg, 132, 133),
            new UpgradeOpportunityRule(true, 27, Race.Zerg, 142),
            new UpgradeOpportunityRule(true, 28, Race.Zerg, new[] { 142 }, new[] { 133 }),
            new UpgradeOpportunityRule(true, 29, Race.Zerg, 135),
            new UpgradeOpportunityRule(true, 30, Race.Zerg, 135),
            new UpgradeOpportunityRule(true, 31, Race.Zerg, 138),
            new UpgradeOpportunityRule(true, 32, Race.Zerg, 138),
            new UpgradeOpportunityRule(true, 52, Race.Zerg, 140),
            new UpgradeOpportunityRule(true, 53, Race.Zerg, 140),
            UpgradeOpportunityRule.UnitMorph(132, Race.Zerg, new[] { 131 }, new[] { 135 }),
            UpgradeOpportunityRule.UnitMorph(133, Race.Zerg, new[] { 132 }, new[] { 136 }),
            new UpgradeOpportunityRule(false, 11, Race.Zerg, 131, 132, 133),
            new UpgradeOpportunityRule(false, 13, Race.Zerg, 138),
            new UpgradeOpportunityRule(false, 14, Race.Zerg, 136),
            new UpgradeOpportunityRule(false, 15, Race.Zerg, 136),
            new UpgradeOpportunityRule(false, 16, Race.Zerg, 136),
            new UpgradeOpportunityRule(false, 17, Race.Zerg, 138),
            new UpgradeOpportunityRule(false, 18, Race.Zerg, 138),
            new UpgradeOpportunityRule(false, 32, Race.Zerg, new[] { 135 }, new[] { 132, 133 }),
            new UpgradeOpportunityRule(true, 5, Race.Protoss, 166),
            new UpgradeOpportunityRule(true, 13, Race.Protoss, 166),
            new UpgradeOpportunityRule(true, 15, Race.Protoss, 166),
            new UpgradeOpportunityRule(true, 6, Race.Protoss, 164),
            new UpgradeOpportunityRule(true, 14, Race.Protoss, 164),
            new UpgradeOpportunityRule(true, 33, Race.Protoss, 164),
            new UpgradeOpportunityRule(true, 34, Race.Protoss, 163),
            new UpgradeOpportunityRule(true, 35, Race.Protoss, 171),
            new UpgradeOpportunityRule(true, 36, Race.Protoss, 171),
            new UpgradeOpportunityRule(true, 37, Race.Protoss, 171),
            new UpgradeOpportunityRule(true, 38, Race.Protoss, 159),
            new UpgradeOpportunityRule(true, 39, Race.Protoss, 159),
            new UpgradeOpportunityRule(true, 40, Race.Protoss, 165),
            new UpgradeOpportunityRule(true, 41, Race.Protoss, 169),
            new UpgradeOpportunityRule(true, 42, Race.Protoss, 169),
            new UpgradeOpportunityRule(true, 43, Race.Protoss, 169),
            new UpgradeOpportunityRule(true, 44, Race.Protoss, 170),
            new UpgradeOpportunityRule(true, 47, Race.Protoss, 169),
            new UpgradeOpportunityRule(true, 49, Race.Protoss, 165),
            new UpgradeOpportunityRule(false, 19, Race.Protoss, 165),
            new UpgradeOpportunityRule(false, 20, Race.Protoss, 165),
            new UpgradeOpportunityRule(false, 21, Race.Protoss, 170),
            new UpgradeOpportunityRule(false, 22, Race.Protoss, 170),
            new UpgradeOpportunityRule(false, 25, Race.Protoss, 169),
            new UpgradeOpportunityRule(false, 27, Race.Protoss, 165),
            new UpgradeOpportunityRule(false, 31, Race.Protoss, 165)
        };

        private static readonly Dictionary<string, Race> UpgradeOpportunityRaceByStateKey =
            UpgradeOpportunityRules
                .GroupBy(rule => rule.StateKey, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First().Race, StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, UpgradeOpportunityRule> UpgradeOpportunityRuleByStateKey =
            UpgradeOpportunityRules
                .GroupBy(rule => rule.StateKey, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        private static string UpgradeStateKey(int index)
        {
            return "upgrade:" + index.ToString(CultureInfo.InvariantCulture);
        }

        private static string TechStateKey(int index)
        {
            return "tech:" + index.ToString(CultureInfo.InvariantCulture);
        }

        private static string UnitStateKey(int index)
        {
            return "unit:" + index.ToString(CultureInfo.InvariantCulture);
        }

        internal static bool IsResearchTechTypeId(int techTypeId)
        {
            return !NonResearchTechTypeIds.Contains(techTypeId);
        }

        private sealed class UpgradeOpportunityRule
        {
            public static UpgradeOpportunityRule UnitMorph(int unitId, Race race, params int[][] requiredBuildingGroups)
            {
                return new UpgradeOpportunityRule(false, unitId, race, UnitStateKey(unitId), requiredBuildingGroups);
            }

            public UpgradeOpportunityRule(bool isUpgrade, int index, Race race, params int[] requiredBuildings)
                : this(isUpgrade, index, race, new[] { requiredBuildings ?? new int[0] })
            {
            }

            public UpgradeOpportunityRule(bool isUpgrade, int index, Race race, params int[][] requiredBuildingGroups)
                : this(isUpgrade, index, race, isUpgrade ? UpgradeStateKey(index) : TechStateKey(index), requiredBuildingGroups)
            {
            }

            private UpgradeOpportunityRule(
                bool isUpgrade,
                int index,
                Race race,
                string stateKey,
                params int[][] requiredBuildingGroups)
            {
                IsUpgrade = isUpgrade;
                Index = index;
                Race = race;
                RequiredBuildingGroups = (requiredBuildingGroups ?? new int[0][])
                    .Where(group => group != null && group.Length > 0)
                    .ToArray();
                StateKey = stateKey;
                IsUnitMorph = !isUpgrade &&
                              !string.IsNullOrEmpty(stateKey) &&
                              stateKey.StartsWith("unit:", StringComparison.OrdinalIgnoreCase);
            }

            public bool IsUpgrade { get; }
            public bool IsUnitMorph { get; }
            public int Index { get; }
            public Race Race { get; }
            public int[][] RequiredBuildingGroups { get; }
            public string StateKey { get; }
        }
    }
}
