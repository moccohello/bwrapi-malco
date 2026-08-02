using System;
using System.Collections.Generic;
using System.Linq;

namespace Malco.Configuration.Models
{
    internal sealed class HudDisplayPreferences
    {
        private readonly HashSet<string> _hiddenItemKeys;
        private readonly HashSet<string> _disabledWidgetKeys;
        private readonly HashSet<string> _disabledAvailableAlertKeys;
        private readonly HashSet<string> _disabledCompletionWarningKeys;
        private readonly Dictionary<int, string> _abilityDisplayModes;

        private HudDisplayPreferences(
            IEnumerable<string> hiddenItemKeys,
            IEnumerable<string> disabledWidgetKeys,
            IEnumerable<string> disabledAvailableAlertKeys,
            IEnumerable<string> disabledCompletionWarningKeys,
            IDictionary<int, string> abilityDisplayModes,
            bool showTransportCargo,
            string workerCountStyle,
            string unitIconSize,
            string buildingIconSize,
            string completedUpgradeIconSize,
            string availableUpgradeIconSize,
            UpgradeCompletionDisplayMode completionDisplayMode,
            int completionCountdownSeconds)
        {
            _hiddenItemKeys = new HashSet<string>(
                hiddenItemKeys ?? Enumerable.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);
            _disabledWidgetKeys = new HashSet<string>(disabledWidgetKeys ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            _disabledAvailableAlertKeys = new HashSet<string>(disabledAvailableAlertKeys ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            _disabledCompletionWarningKeys = new HashSet<string>(disabledCompletionWarningKeys ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            _abilityDisplayModes = new Dictionary<int, string>(abilityDisplayModes ?? new Dictionary<int, string>());
            ShowTransportCargo = showTransportCargo;
            WorkerCountStyle = MalcoPreferenceValues.NormalizeWorkerCountStyle(workerCountStyle);
            UnitIconSize = MalcoPreferenceValues.NormalizeIconSize(unitIconSize, MalcoPreferenceValues.IconLarge);
            BuildingIconSize = MalcoPreferenceValues.NormalizeIconSize(buildingIconSize, MalcoPreferenceValues.IconLarge);
            CompletedUpgradeIconSize = MalcoPreferenceValues.NormalizeIconSize(completedUpgradeIconSize, MalcoPreferenceValues.IconMedium);
            AvailableUpgradeIconSize = MalcoPreferenceValues.NormalizeIconSize(availableUpgradeIconSize, MalcoPreferenceValues.IconSmall);
            CompletionDisplayMode = completionDisplayMode;
            CompletionCountdownSeconds = MalcoPreferenceValues.NormalizeCompletionCountdownSeconds(completionCountdownSeconds);
        }

        public bool IsItemShown(string key) =>
            string.IsNullOrEmpty(key) || !_hiddenItemKeys.Contains(key);

        public bool IsWidgetEnabled(string key) =>
            string.IsNullOrEmpty(key) || !_disabledWidgetKeys.Contains(key);

        public bool IsAvailableAlertEnabled(string key) =>
            string.IsNullOrEmpty(key) || !_disabledAvailableAlertKeys.Contains(key);

        public bool IsCompletionWarningEnabled(string key) =>
            string.IsNullOrEmpty(key) || !_disabledCompletionWarningKeys.Contains(key);

        public UpgradeCompletionDisplayMode CompletionDisplayMode { get; }

        public int CompletionCountdownSeconds { get; }

        public bool ShowTransportCargo { get; }

        public string WorkerCountStyle { get; }

        public string UnitIconSize { get; }

        public string BuildingIconSize { get; }

        public string CompletedUpgradeIconSize { get; }

        public string AvailableUpgradeIconSize { get; }

        public string AbilityDisplayMode(int unitId)
        {
            string mode;
            return _abilityDisplayModes.TryGetValue(unitId, out mode)
                ? mode
                : MalcoPreferenceValues.AbilityHidden;
        }

        public static HudDisplayPreferences FromLayout(HudLayoutConfig layout)
        {
            var hiddenKeys = layout != null && layout.ItemSettings != null
                ? layout.ItemSettings
                    .Where(entry => entry.Value != null && !entry.Value.Show)
                    .Select(entry => entry.Key)
                    .ToArray()
                : Array.Empty<string>();
            var disabledWidgets = layout != null && layout.Widgets != null
                ? layout.Widgets.Where(entry => entry.Value != null && !entry.Value.Enabled).Select(entry => entry.Key).ToArray()
                : Array.Empty<string>();
            var disabledAvailableAlerts = layout != null && layout.ItemSettings != null
                ? layout.ItemSettings.Where(entry => entry.Value != null && !entry.Value.AvailableAlert).Select(entry => entry.Key).ToArray()
                : Array.Empty<string>();
            var disabledCompletionWarnings = layout != null && layout.ItemSettings != null
                ? layout.ItemSettings.Where(entry => entry.Value != null && !entry.Value.CompletionAlert).Select(entry => entry.Key).ToArray()
                : Array.Empty<string>();
            var abilityModes = new Dictionary<int, string>();
            if (layout != null && layout.AbilityDisplayModes != null)
            {
                foreach (var entry in layout.AbilityDisplayModes)
                {
                    int unitId;
                    if (entry.Key != null && entry.Key.StartsWith("unit:", StringComparison.OrdinalIgnoreCase) &&
                        int.TryParse(entry.Key.Substring(5), out unitId))
                        abilityModes[unitId] = MalcoPreferenceValues.NormalizeAbilityDisplayModeForUnit(unitId, entry.Value);
                }
            }
            return new HudDisplayPreferences(
                hiddenKeys,
                disabledWidgets,
                disabledAvailableAlerts,
                disabledCompletionWarnings,
                abilityModes,
                layout == null || layout.ShowTransportCargo,
                layout != null ? layout.WorkerCountStyle : MalcoPreferenceValues.WorkerCountClassicGreen,
                layout != null ? layout.UnitIconSize : MalcoPreferenceValues.IconLarge,
                layout != null ? layout.BuildingIconSize : MalcoPreferenceValues.IconLarge,
                layout != null ? layout.CompletedUpgradeIconSize : MalcoPreferenceValues.IconMedium,
                layout != null ? layout.AvailableUpgradeIconSize : MalcoPreferenceValues.IconSmall,
                MalcoPreferenceValues.ParseCompletionMode(layout != null ? layout.CompletionDisplayMode : null),
                layout != null ? layout.CompletionCountdownSeconds : MalcoPreferenceValues.DefaultCompletionCountdownSeconds);
        }
    }
}
