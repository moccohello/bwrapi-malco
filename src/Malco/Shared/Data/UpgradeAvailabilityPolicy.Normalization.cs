using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Malco.Models;

namespace Malco.Data
{
    internal static partial class UpgradeAvailabilityPolicy
    {
        internal static List<UpgradeState> NormalizeUpgradeStatesForRace(
            Race race,
            IEnumerable<UpgradeState> states)
        {
            return SortUpgradeStates(FilterUpgradeStatesForRace(race, states));
        }

        internal static List<UpgradeState> NormalizeAvailableUpgradeStatesForRace(
            Race race,
            IEnumerable<UpgradeState> states,
            IEnumerable<UpgradeState> knownStates)
        {
            return MergeAvailableUpgradeStates(race, states, knownStates);
        }

        private static List<UpgradeState> MergeAvailableUpgradeStates(
            Race race,
            IEnumerable<UpgradeState> states,
            IEnumerable<UpgradeState> knownStates)
        {
            var byKey = new Dictionary<string, UpgradeState>(StringComparer.OrdinalIgnoreCase);
            var blocked = BuildBlockedAvailableStateKeys(knownStates);
            foreach (var state in states ?? new UpgradeState[0])
            {
                if (state == null || state.IsComplete || !state.IsAvailable)
                {
                    continue;
                }

                var key = GetUpgradeStateKey(state);
                if (string.IsNullOrEmpty(key) ||
                    !IsStateKeyAllowedForRace(key, race) ||
                    IsNonResearchTechStateKey(key) ||
                    blocked.Contains(key))
                {
                    continue;
                }

                byKey[key] = state;
            }

            return byKey.Values.OrderBy(state => state.Name).ToList();
        }

        private static HashSet<string> BuildBlockedAvailableStateKeys(IEnumerable<UpgradeState> knownStates)
        {
            var blocked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var state in knownStates ?? new UpgradeState[0])
            {
                if (state == null)
                {
                    continue;
                }

                var key = GetUpgradeStateKey(state);
                if (!string.IsNullOrEmpty(key) &&
                    (state.IsInProgress ||
                     state.SecondsRemainingPrecise > 0d ||
                     state.SecondsRemaining > 0 ||
                     (state.IsComplete && IsAtMaxLevel(key, state.Level))))
                {
                    blocked.Add(key);
                }
            }
            return blocked;
        }

        private static bool IsAtMaxLevel(string key, int level)
        {
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }
            if (key.StartsWith("tech:", StringComparison.OrdinalIgnoreCase))
            {
                return level >= 1;
            }
            if (!key.StartsWith("upgrade:", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            int index;
            return int.TryParse(
                       key.Substring("upgrade:".Length),
                       NumberStyles.Integer,
                       CultureInfo.InvariantCulture,
                       out index) &&
                   level >= Math.Max(1, BwapiBroodWarTables.GetUpgradeMaxLevel(index));
        }

        private static List<UpgradeState> FilterUpgradeStatesForRace(Race race, IEnumerable<UpgradeState> states)
        {
            return (states ?? new UpgradeState[0])
                .Where(state => state != null &&
                    IsStateKeyAllowedForRace(GetUpgradeStateKey(state), race) &&
                    !IsNonResearchTechStateKey(GetUpgradeStateKey(state)))
                .ToList();
        }

        private static bool IsStateKeyAllowedForRace(string stateKey, Race race)
        {
            if (race == Race.Unknown || string.IsNullOrEmpty(stateKey))
            {
                return true;
            }
            Race stateRace;
            if (UpgradeOpportunityRaceByStateKey.TryGetValue(stateKey, out stateRace))
            {
                return stateRace == race;
            }
            return !IsCanonicalBwapiStateKey(stateKey);
        }

        private static bool IsNonResearchTechStateKey(string stateKey)
        {
            int index;
            return !string.IsNullOrEmpty(stateKey) &&
                   stateKey.StartsWith("tech:", StringComparison.OrdinalIgnoreCase) &&
                   int.TryParse(
                       stateKey.Substring("tech:".Length),
                       NumberStyles.Integer,
                       CultureInfo.InvariantCulture,
                       out index) &&
                   NonResearchTechTypeIds.Contains(index);
        }

        private static bool IsCanonicalBwapiStateKey(string stateKey)
        {
            if (stateKey == null)
            {
                return false;
            }
            int index;
            if (stateKey.StartsWith("upgrade:", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(stateKey.Substring("upgrade:".Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out index))
            {
                return index >= 0 && index <= BwapiBroodWarTables.UpgradeTypeLastDataIndex;
            }
            if (stateKey.StartsWith("tech:", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(stateKey.Substring("tech:".Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out index))
            {
                return index >= 0 && index <= BwapiBroodWarTables.TechTypeLastDataIndex;
            }
            if (stateKey.StartsWith("unit:", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(stateKey.Substring("unit:".Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out index))
            {
                return BwapiBroodWarTables.GetUnitTypeInfo(index).IsBuilding;
            }
            return false;
        }

        private static List<UpgradeState> SortUpgradeStates(IEnumerable<UpgradeState> states)
        {
            return (states ?? new UpgradeState[0])
                .Where(state => state != null && !string.IsNullOrEmpty(state.Name))
                .GroupBy(state => GetUpgradeStateKey(state) + "|" + state.IsComplete.ToString(CultureInfo.InvariantCulture))
                .Select(group => group.OrderByDescending(state => state.SecondsRemainingPrecise).First())
                .OrderBy(state => state.IsComplete)
                .ThenBy(state => state.Name)
                .ToList();
        }

        private static string GetUpgradeStateKey(UpgradeState state)
        {
            if (state == null)
            {
                return string.Empty;
            }
            if (!string.IsNullOrEmpty(state.StateKey))
            {
                return state.StateKey;
            }
            var name = state.Name ?? string.Empty;
            if (name.StartsWith("Upgrade ", StringComparison.OrdinalIgnoreCase))
            {
                int index;
                var rawName = StripUpgradeLevel(name.Substring("Upgrade ".Length));
                return BwapiBroodWarTables.TryGetUpgradeIndex(rawName, out index) ? UpgradeStateKey(index) : string.Empty;
            }
            if (name.StartsWith("Tech ", StringComparison.OrdinalIgnoreCase))
            {
                int index;
                var rawName = name.Substring("Tech ".Length).Trim();
                return BwapiBroodWarTables.TryGetTechIndex(rawName, out index) ? TechStateKey(index) : string.Empty;
            }
            var trimmedName = StripUpgradeLevel(name);
            int rawIndex;
            if (BwapiBroodWarTables.TryGetUpgradeIndex(trimmedName, out rawIndex))
            {
                return UpgradeStateKey(rawIndex);
            }
            if (BwapiBroodWarTables.TryGetTechIndex(name.Trim(), out rawIndex))
            {
                return TechStateKey(rawIndex);
            }
            return string.Empty;
        }

        private static string BuildUpgradeStateName(int upgradeId, int level)
        {
            var name = "Upgrade " + BwapiBroodWarTables.GetUpgradeTypeName(upgradeId);
            return level > 0 && BwapiBroodWarTables.GetUpgradeMaxLevel(upgradeId) > 1
                ? name + " +" + level.ToString(CultureInfo.InvariantCulture)
                : name;
        }

        private static string StripUpgradeLevel(string name)
        {
            var marker = (name ?? string.Empty).LastIndexOf(" +", StringComparison.Ordinal);
            return marker >= 0 ? name.Substring(0, marker).Trim() : (name ?? string.Empty).Trim();
        }
    }
}
