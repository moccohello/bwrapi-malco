using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Malco.Data;
using Malco.Models;

namespace Malco.Presentation.Hud.Upgrades
{
    internal static class UpgradePresentationIdentity
    {
        private static readonly Regex LevelSuffix = new Regex(@"\+(\d+)$", RegexOptions.Compiled);

        public static string ForState(UpgradeState state)
        {
            if (state == null) return string.Empty;
            if (!string.IsNullOrEmpty(state.StateKey)) return state.StateKey;
            var name = state.Name ?? string.Empty;
            if (name.StartsWith("Upgrade ", StringComparison.OrdinalIgnoreCase))
            {
                var rawName = StripUpgradeLevel(name.Substring("Upgrade ".Length));
                int index;
                return BwapiBroodWarTables.TryGetUpgradeIndex(rawName, out index)
                    ? "upgrade:" + index.ToString(CultureInfo.InvariantCulture)
                    : string.Empty;
            }
            if (name.StartsWith("Tech ", StringComparison.OrdinalIgnoreCase))
            {
                int index;
                return BwapiBroodWarTables.TryGetTechIndex(name.Substring("Tech ".Length), out index)
                    ? "tech:" + index.ToString(CultureInfo.InvariantCulture)
                    : string.Empty;
            }
            return string.Empty;
        }

        public static string BuildCompletedKey(IEnumerable<UpgradeState> states) => string.Join(
            "|",
            (states ?? Array.Empty<UpgradeState>()).Select(BuildStatePart));

        public static string BuildAvailableKey(Race race, IEnumerable<UpgradeState> states) =>
            race + ":" + string.Join("|", (states ?? Array.Empty<UpgradeState>()).Select(BuildStatePart));

        public static string WarningKey(UpgradeState state)
        {
            var key = ForState(state);
            if (!string.IsNullOrEmpty(key)) return key;
            return state != null && !string.IsNullOrEmpty(state.Name) ? state.Name : "warning";
        }

        public static double RawRemainingSeconds(UpgradeState state)
        {
            if (state == null) return 0d;
            return state.SecondsRemainingPrecise > 0d
                ? state.SecondsRemainingPrecise
                : Math.Max(0, state.SecondsRemaining);
        }

        public static bool IsInCompletionWarningWindow(UpgradeState state, double windowSeconds)
        {
            var remaining = RawRemainingSeconds(state);
            return remaining > 0d && remaining <= windowSeconds;
        }

        public static string FormatRemainingSeconds(double seconds)
        {
            return Math.Max(0d, seconds).ToString("0.0", CultureInfo.InvariantCulture) + "s";
        }

        public static string CompletedValue(UpgradeState state)
        {
            if (!ShouldShowCompletedLevel(state)) return string.Empty;
            var match = LevelSuffix.Match(state.Name ?? string.Empty);
            if (match.Success) return match.Groups[1].Value;
            return state.Level > 0 ? state.Level.ToString(CultureInfo.InvariantCulture) : string.Empty;
        }

        public static string FallbackLabel(UpgradeState state)
        {
            var name = state != null ? state.Name : null;
            if (string.IsNullOrWhiteSpace(name)) return "?";
            if (name.StartsWith("Upgrade ", StringComparison.OrdinalIgnoreCase)) name = name.Substring("Upgrade ".Length);
            else if (name.StartsWith("Tech ", StringComparison.OrdinalIgnoreCase)) name = name.Substring("Tech ".Length);
            name = LevelSuffix.Replace(name, string.Empty).Trim();
            foreach (var prefix in new[] { "Terran_", "Zerg_", "Protoss_", "Terran-", "Zerg-", "Protoss-" })
            {
                if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                name = name.Substring(prefix.Length).Trim();
                break;
            }
            foreach (var ch in name)
            {
                if (char.IsLetterOrDigit(ch)) return char.ToUpperInvariant(ch).ToString(CultureInfo.InvariantCulture);
            }
            return "?";
        }

        public static string CompactName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "-";
            if (name.StartsWith("Upgrade ", StringComparison.OrdinalIgnoreCase)) return name.Substring("Upgrade ".Length).Replace('_', ' ');
            if (name.StartsWith("Tech ", StringComparison.OrdinalIgnoreCase)) return name.Substring("Tech ".Length).Replace('_', ' ');
            return name.Replace('_', ' ');
        }

        private static string BuildStatePart(UpgradeState state) => ForState(state) + ":" +
            state.Level.ToString(CultureInfo.InvariantCulture) + ":" +
            state.IsComplete.ToString(CultureInfo.InvariantCulture) + ":" +
            state.IsInProgress.ToString(CultureInfo.InvariantCulture) + ":" +
            state.IsAvailable.ToString(CultureInfo.InvariantCulture) + ":" +
            state.IsBlocked.ToString(CultureInfo.InvariantCulture);

        private static bool ShouldShowCompletedLevel(UpgradeState state)
        {
            var name = state != null ? state.Name : null;
            if (string.IsNullOrEmpty(name) || name.IndexOf("Upgrade ", StringComparison.OrdinalIgnoreCase) != 0) return false;
            var match = LevelSuffix.Match(name);
            var typeName = match.Success
                ? name.Substring("Upgrade ".Length, match.Index - "Upgrade ".Length).Trim()
                : name.Substring("Upgrade ".Length).Trim();
            int upgradeIndex;
            if (!BwapiBroodWarTables.TryGetUpgradeIndex(typeName, out upgradeIndex)) return true;
            return BwapiBroodWarTables.GetUpgradeMaxLevel(upgradeIndex) > 1;
        }

        private static string StripUpgradeLevel(string name)
        {
            var marker = name.LastIndexOf(" +", StringComparison.Ordinal);
            return marker >= 0 ? name.Substring(0, marker).Trim() : name.Trim();
        }
    }
}
