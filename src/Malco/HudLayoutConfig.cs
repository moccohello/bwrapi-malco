using System;
using System.Collections.Generic;
using System.Globalization;
using Malco.Configuration.Models;
using Malco.Models;

namespace Malco
{
    internal sealed class HudLayoutConfig
    {
        public HudLayoutConfig()
        {
            SchemaVersion = Configuration.HudLayoutFileStore.CurrentSchemaVersion;
            Language = MalcoPreferenceValues.NormalizeLanguage(null);
            CompletionDisplayMode = MalcoPreferenceValues.Progress;
            CompletionCountdownSeconds = MalcoPreferenceValues.DefaultCompletionCountdownSeconds;
            UnitIconSize = MalcoPreferenceValues.IconLarge;
            BuildingIconSize = MalcoPreferenceValues.IconLarge;
            CompletedUpgradeIconSize = MalcoPreferenceValues.IconMedium;
            AvailableUpgradeIconSize = MalcoPreferenceValues.IconSmall;
            WorkerCountStyle = MalcoPreferenceValues.WorkerCountClassicGreen;
            Widgets = new Dictionary<string, WidgetLayout>(StringComparer.OrdinalIgnoreCase);
            ItemSettings = new Dictionary<string, HudItemSetting>(StringComparer.OrdinalIgnoreCase);
            AbilityDisplayModes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            ShowTransportCargo = true;
        }

        public static HudLayoutConfig CreateDefault()
        {
            var result = new HudLayoutConfig();
            result.SchemaVersion = Configuration.HudLayoutFileStore.CurrentSchemaVersion;
            foreach (var definition in HudWidgetRegistry.EditorFeatures())
            {
                var widget = new WidgetLayout
                {
                    Enabled = definition.EnabledByDefault,
                    X = definition.X,
                    Y = definition.Y,
                    Width = definition.Width,
                    Height = definition.Height
                };
                widget.UpdateRelativeBounds(1280d, 720d);
                result.Widgets[definition.Key] = widget;
            }
            result.SetItemShown("unit:64", false);
            result.SetAvailableUpgradeAlert("upgrade:24", false);
            result.SetAbilityDisplayMode("unit:9", "tech:7");
            result.SetAbilityDisplayMode("unit:67", "tech:19");
            result.SetAbilityDisplayMode("unit:71", MalcoPreferenceValues.AbilityEnergy);
            result.SetAbilityDisplayMode("unit:46", "tech:14");
            result.SetAbilityDisplayMode("unit:45", "tech:13");
            result.SetAbilityDisplayMode("unit:107", MalcoPreferenceValues.AbilityEnergy);
            return result;
        }

        public int SchemaVersion { get; set; }

        public string Language { get; set; }

        public string CompletionDisplayMode { get; set; }

        public int CompletionCountdownSeconds { get; set; }

        public string UnitIconSize { get; set; }

        public string BuildingIconSize { get; set; }

        public string CompletedUpgradeIconSize { get; set; }

        public string AvailableUpgradeIconSize { get; set; }

        public string WorkerCountStyle { get; set; }

        public Dictionary<string, WidgetLayout> Widgets { get; set; }

        public Dictionary<string, HudItemSetting> ItemSettings { get; set; }

        public Dictionary<string, string> AbilityDisplayModes { get; set; }

        public bool ShowTransportCargo { get; set; }

        public string GetIconSize(string featureKey)
        {
            if (string.Equals(featureKey, HudWidgetRegistry.Units, StringComparison.OrdinalIgnoreCase))
                return MalcoPreferenceValues.NormalizeIconSize(UnitIconSize, MalcoPreferenceValues.IconLarge);
            if (string.Equals(featureKey, HudWidgetRegistry.Buildings, StringComparison.OrdinalIgnoreCase))
                return MalcoPreferenceValues.NormalizeIconSize(BuildingIconSize, MalcoPreferenceValues.IconLarge);
            if (string.Equals(featureKey, HudWidgetRegistry.Upgrades, StringComparison.OrdinalIgnoreCase))
                return MalcoPreferenceValues.NormalizeIconSize(CompletedUpgradeIconSize, MalcoPreferenceValues.IconMedium);
            if (string.Equals(featureKey, HudWidgetRegistry.AvailableUpgrades, StringComparison.OrdinalIgnoreCase))
                return MalcoPreferenceValues.NormalizeIconSize(AvailableUpgradeIconSize, MalcoPreferenceValues.IconSmall);
            return MalcoPreferenceValues.IconLarge;
        }

        public bool SetIconSize(string featureKey, string size)
        {
            string normalized;
            if (string.Equals(featureKey, HudWidgetRegistry.Units, StringComparison.OrdinalIgnoreCase))
            {
                if (!TryUpdateIconSize(UnitIconSize, size, MalcoPreferenceValues.IconLarge, out normalized))
                    return false;
                UnitIconSize = normalized;
                return true;
            }
            if (string.Equals(featureKey, HudWidgetRegistry.Buildings, StringComparison.OrdinalIgnoreCase))
            {
                if (!TryUpdateIconSize(BuildingIconSize, size, MalcoPreferenceValues.IconLarge, out normalized))
                    return false;
                BuildingIconSize = normalized;
                return true;
            }
            if (string.Equals(featureKey, HudWidgetRegistry.Upgrades, StringComparison.OrdinalIgnoreCase))
            {
                if (!TryUpdateIconSize(CompletedUpgradeIconSize, size, MalcoPreferenceValues.IconMedium, out normalized))
                    return false;
                CompletedUpgradeIconSize = normalized;
                return true;
            }
            if (string.Equals(featureKey, HudWidgetRegistry.AvailableUpgrades, StringComparison.OrdinalIgnoreCase))
            {
                if (!TryUpdateIconSize(AvailableUpgradeIconSize, size, MalcoPreferenceValues.IconSmall, out normalized))
                    return false;
                AvailableUpgradeIconSize = normalized;
                return true;
            }

            return false;
        }

        public string GetAbilityDisplayMode(int unitId)
        {
            string value;
            var key = "unit:" + unitId.ToString(CultureInfo.InvariantCulture);
            return AbilityDisplayModes != null && AbilityDisplayModes.TryGetValue(key, out value)
                ? MalcoPreferenceValues.NormalizeAbilityDisplayModeForUnit(unitId, value)
                : MalcoPreferenceValues.AbilityHidden;
        }

        public bool SetAbilityDisplayMode(string key, string mode)
        {
            int unitId;
            if (!TryParseAbilityKey(key, out unitId))
            {
                return false;
            }

            var canonicalKey = "unit:" + unitId.ToString(CultureInfo.InvariantCulture);
            var normalized = MalcoPreferenceValues.NormalizeAbilityDisplayModeForUnit(unitId, mode);
            string current;
            if (AbilityDisplayModes != null &&
                AbilityDisplayModes.TryGetValue(canonicalKey, out current) &&
                string.Equals(
                    MalcoPreferenceValues.NormalizeAbilityDisplayModeForUnit(unitId, current),
                    normalized,
                    StringComparison.Ordinal))
            {
                return false;
            }
            if ((AbilityDisplayModes == null || !AbilityDisplayModes.ContainsKey(canonicalKey)) &&
                string.Equals(normalized, MalcoPreferenceValues.AbilityHidden, StringComparison.Ordinal))
            {
                return false;
            }

            if (AbilityDisplayModes == null)
            {
                AbilityDisplayModes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
            AbilityDisplayModes[canonicalKey] = normalized;
            return true;
        }

        private static bool TryParseAbilityKey(string key, out int unitId)
        {
            unitId = 0;
            return !string.IsNullOrWhiteSpace(key) &&
                   key.StartsWith("unit:", StringComparison.OrdinalIgnoreCase) &&
                   int.TryParse(key.Substring(5), NumberStyles.Integer, CultureInfo.InvariantCulture, out unitId) &&
                   AbilityCatalog.Find(unitId) != null;
        }

        private static bool TryUpdateIconSize(
            string current,
            string requested,
            string fallback,
            out string normalized)
        {
            var normalizedCurrent = MalcoPreferenceValues.NormalizeIconSize(current, fallback);
            normalized = MalcoPreferenceValues.NormalizeIconSize(requested, normalizedCurrent);
            return !string.Equals(normalizedCurrent, normalized, StringComparison.Ordinal);
        }

        public WidgetLayout GetOrCreate(
            string key,
            double x,
            double y,
            double width,
            double height,
            bool enabledByDefault)
        {
            WidgetLayout layout;
            if (Widgets == null)
            {
                Widgets = new Dictionary<string, WidgetLayout>(StringComparer.OrdinalIgnoreCase);
            }

            if (!Widgets.TryGetValue(key, out layout) || layout == null)
            {
                layout = new WidgetLayout
                {
                    Enabled = enabledByDefault,
                    X = x,
                    Y = y,
                    Width = width,
                    Height = height
                };
                Widgets[key] = layout;
            }

            layout.Normalize(
                x,
                y,
                width,
                height,
                HudWidgetLayoutPolicy.MinimumWidth(key),
                HudWidgetLayoutPolicy.MinimumHeight(key));
            return layout;
        }

        public bool IsAvailableUpgradeAlertEnabled(string key)
        {
            HudItemSetting setting;
            return string.IsNullOrEmpty(key) ||
                   ItemSettings == null ||
                   !ItemSettings.TryGetValue(key, out setting) ||
                   setting == null ||
                   setting.AvailableAlert;
        }

        public bool IsCompletionWarningEnabled(string key)
        {
            HudItemSetting setting;
            return string.IsNullOrEmpty(key) ||
                   ItemSettings == null ||
                   !ItemSettings.TryGetValue(key, out setting) ||
                   setting == null ||
                   setting.CompletionAlert;
        }

        public void SetAvailableUpgradeAlert(string key, bool enabled)
        {
            if (!string.IsNullOrEmpty(key))
            {
                GetOrCreateItemSetting(key).AvailableAlert = enabled;
            }

        }

        public void SetCompletionWarning(string key, bool enabled)
        {
            if (!string.IsNullOrEmpty(key))
            {
                GetOrCreateItemSetting(key).CompletionAlert = enabled;
            }

        }

        public bool IsItemShown(string key)
        {
            HudItemSetting setting;
            return string.IsNullOrEmpty(key) ||
                   ItemSettings == null ||
                   !ItemSettings.TryGetValue(key, out setting) ||
                   setting == null ||
                   setting.Show;
        }

        public void SetItemShown(string key, bool enabled)
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            GetOrCreateItemSetting(key).Show = enabled;
        }

        public HudItemSetting GetOrCreateItemSetting(string key)
        {
            if (ItemSettings == null)
            {
                ItemSettings = new Dictionary<string, HudItemSetting>(StringComparer.OrdinalIgnoreCase);
            }

            HudItemSetting setting;
            if (!ItemSettings.TryGetValue(key, out setting) || setting == null)
            {
                setting = new HudItemSetting();
                ItemSettings[key] = setting;
            }

            return setting;
        }
    }

}
