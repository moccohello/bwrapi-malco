using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Malco.Models;

namespace Malco.Data
{
    internal static partial class UpgradeAvailabilityPolicy
    {
        internal static List<UpgradeState> BuildPrerequisiteAvailableUpgradeStates(
            Race race,
            IEnumerable<UnitCount> buildingCounts,
            IEnumerable<UpgradeState> knownStates)
        {
            var countList = (buildingCounts ?? new UnitCount[0])
                .Where(count => count != null)
                .ToList();
            var completedBuildings = new HashSet<int>(
                countList
                    .Where(count => count.CompletedCount > 0)
                    .Select(count => count.UnitId));
            if (completedBuildings.Count == 0)
            {
                return new List<UpgradeState>();
            }

            var completedBuildingCounts = countList
                .GroupBy(count => count.UnitId)
                .ToDictionary(group => group.Key, group => group.Sum(count => count.CompletedCount));
            var completedLevels = BuildCompletedUpgradeLevels(knownStates);
            var busyProducerCounts = BuildBusyProducerCounts(knownStates);
            return UpgradeOpportunityRules
                .Where(rule => rule.Race == race && HasRequiredBuildings(rule, completedBuildings))
                .Select(rule => BuildPrerequisiteAvailableUpgradeState(
                    rule,
                    completedBuildings,
                    completedBuildingCounts,
                    completedLevels,
                    busyProducerCounts))
                .Where(state => state != null)
                .ToList();
        }

        private static UpgradeState BuildPrerequisiteAvailableUpgradeState(
            UpgradeOpportunityRule rule,
            HashSet<int> completedBuildings,
            IDictionary<int, int> completedBuildingCounts,
            IDictionary<string, int> completedLevels,
            IDictionary<string, int> busyProducerCounts)
        {
            if (rule != null && !rule.IsUpgrade && NonResearchTechTypeIds.Contains(rule.Index))
            {
                return null;
            }

            var level = 0;
            var displayLevel = 0;
            if (rule.IsUpgrade)
            {
                completedLevels.TryGetValue(rule.StateKey, out level);
                var maxLevel = Math.Max(1, BwapiBroodWarTables.GetUpgradeMaxLevel(rule.Index));
                if (level >= maxLevel)
                {
                    return null;
                }
                var nextLevel = level + 1;
                if (!HasUpgradeTierRequirement(rule, nextLevel, completedBuildings))
                {
                    return null;
                }
                displayLevel = level > 0 ? nextLevel : 0;
            }
            else if (rule.IsUnitMorph && HasCompletedMorphTarget(rule, completedBuildings))
            {
                return null;
            }

            return new UpgradeState
            {
                StateKey = rule.StateKey,
                Name = BuildOpportunityStateName(rule, displayLevel),
                Level = displayLevel,
                ProgressPercent = 0d,
                SecondsRemaining = 0,
                SecondsRemainingPrecise = 0d,
                IsComplete = false,
                IsInProgress = false,
                IsAvailable = true,
                IsBlocked = IsProducerBusy(rule, completedBuildingCounts, busyProducerCounts)
            };
        }

        private static string BuildOpportunityStateName(UpgradeOpportunityRule rule, int displayLevel)
        {
            if (rule == null)
            {
                return "Upgrade";
            }
            if (rule.IsUpgrade)
            {
                return BuildUpgradeStateName(rule.Index, displayLevel);
            }
            if (rule.IsUnitMorph)
            {
                return "Morph " + BwapiBroodWarTables.GetUnitTypeInfo(rule.Index).Name;
            }
            return "Tech " + BwapiBroodWarTables.GetTechTypeName(rule.Index);
        }

        private static bool HasCompletedMorphTarget(UpgradeOpportunityRule rule, HashSet<int> completedBuildings)
        {
            if (rule == null || completedBuildings == null)
            {
                return false;
            }
            if (rule.Index == 132)
            {
                return completedBuildings.Contains(132) || completedBuildings.Contains(133);
            }
            return completedBuildings.Contains(rule.Index);
        }

        private static Dictionary<string, int> BuildCompletedUpgradeLevels(IEnumerable<UpgradeState> knownStates)
        {
            var levels = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var state in knownStates ?? new UpgradeState[0])
            {
                if (state == null || !state.IsComplete)
                {
                    continue;
                }
                var key = GetUpgradeStateKey(state);
                if (string.IsNullOrEmpty(key) || !key.StartsWith("upgrade:", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                int current;
                if (!levels.TryGetValue(key, out current) || state.Level > current)
                {
                    levels[key] = state.Level;
                }
            }
            return levels;
        }

        private static Dictionary<string, int> BuildBusyProducerCounts(IEnumerable<UpgradeState> knownStates)
        {
            var busy = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var state in knownStates ?? new UpgradeState[0])
            {
                if (state == null ||
                    !(state.IsInProgress || state.SecondsRemainingPrecise > 0d || state.SecondsRemaining > 0))
                {
                    continue;
                }
                var key = GetUpgradeStateKey(state);
                UpgradeOpportunityRule rule;
                if (string.IsNullOrEmpty(key) || !UpgradeOpportunityRuleByStateKey.TryGetValue(key, out rule))
                {
                    continue;
                }
                var producerKey = BuildProducerGroupKey(rule);
                if (!string.IsNullOrEmpty(producerKey))
                {
                    int count;
                    busy.TryGetValue(producerKey, out count);
                    busy[producerKey] = count + 1;
                }
            }
            return busy;
        }

        private static bool IsProducerBusy(
            UpgradeOpportunityRule rule,
            IDictionary<int, int> completedBuildingCounts,
            IDictionary<string, int> busyProducerCounts)
        {
            var producerKey = BuildProducerGroupKey(rule);
            int busyCount;
            if (string.IsNullOrEmpty(producerKey) ||
                busyProducerCounts == null ||
                !busyProducerCounts.TryGetValue(producerKey, out busyCount) ||
                busyCount <= 0)
            {
                return false;
            }
            var completedProducers = 0;
            foreach (var unitId in GetProducerGroup(rule))
            {
                int count;
                if (completedBuildingCounts != null && completedBuildingCounts.TryGetValue(unitId, out count))
                {
                    completedProducers += count;
                }
            }
            return completedProducers <= busyCount;
        }

        private static string BuildProducerGroupKey(UpgradeOpportunityRule rule)
        {
            return string.Join(",", GetProducerGroup(rule)
                .OrderBy(unitId => unitId)
                .Select(unitId => unitId.ToString(CultureInfo.InvariantCulture)));
        }

        private static IEnumerable<int> GetProducerGroup(UpgradeOpportunityRule rule)
        {
            if (rule == null || rule.RequiredBuildingGroups == null || rule.RequiredBuildingGroups.Length == 0)
            {
                return new int[0];
            }
            var producerGroupIndex = rule.Race == Race.Terran && rule.RequiredBuildingGroups.Length > 1 ? 1 : 0;
            return rule.RequiredBuildingGroups[producerGroupIndex] ?? new int[0];
        }

        private static bool HasUpgradeTierRequirement(
            UpgradeOpportunityRule rule,
            int nextLevel,
            HashSet<int> completedBuildings)
        {
            if (rule == null || nextLevel <= 1)
            {
                return true;
            }
            if (rule.Race == Race.Terran)
            {
                return TerranThreeLevelUpgradeIds.Contains(rule.Index) && completedBuildings.Contains(116);
            }
            if (rule.Race == Race.Zerg && ZergHatcheryTierUpgradeIds.Contains(rule.Index))
            {
                return nextLevel == 2
                    ? completedBuildings.Contains(132) || completedBuildings.Contains(133)
                    : completedBuildings.Contains(133);
            }
            if (rule.Race == Race.Protoss)
            {
                if (ProtossGroundThreeLevelUpgradeIds.Contains(rule.Index))
                {
                    return completedBuildings.Contains(165);
                }
                if (ProtossAirThreeLevelUpgradeIds.Contains(rule.Index))
                {
                    return completedBuildings.Contains(169);
                }
            }
            return false;
        }

        private static bool HasRequiredBuildings(UpgradeOpportunityRule rule, HashSet<int> completedBuildings)
        {
            return rule != null &&
                   rule.RequiredBuildingGroups != null &&
                   rule.RequiredBuildingGroups.Length > 0 &&
                   rule.RequiredBuildingGroups.All(group => group != null && group.Any(completedBuildings.Contains));
        }
    }
}
