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
            Language = MalcoPreferenceValues.English;
            CompletionDisplayMode = MalcoPreferenceValues.Progress;
            CompletionCountdownSeconds = MalcoPreferenceValues.DefaultCompletionCountdownSeconds;
            UnitIconSize = MalcoPreferenceValues.IconMedium;
            BuildingIconSize = MalcoPreferenceValues.IconMedium;
            CompletedUpgradeIconSize = MalcoPreferenceValues.IconMedium;
            AvailableUpgradeIconSize = MalcoPreferenceValues.IconMedium;
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
            foreach (var key in DefaultHiddenItemKeys)
            {
                result.SetItemShown(key, false);
            }
            foreach (var key in DefaultDisabledAvailableAlertKeys)
            {
                result.SetAvailableUpgradeAlert(key, false);
            }
            foreach (var key in DefaultDisabledCompletionAlertKeys)
            {
                result.SetCompletionWarning(key, false);
            }
            result.SetAbilityDisplayMode("unit:9", "tech:7");
            result.SetAbilityDisplayMode("unit:67", "tech:19");
            result.SetAbilityDisplayMode("unit:71", MalcoPreferenceValues.AbilityEnergy);
            result.SetAbilityDisplayMode("unit:46", "tech:14");
            result.SetAbilityDisplayMode("unit:45", "tech:13");
            result.SetAbilityDisplayMode("unit:107", MalcoPreferenceValues.AbilityEnergy);
            result.SetAbilityDisplayMode("unit:12", "tech:8");
            result.SetAbilityDisplayMode("unit:1", MalcoPreferenceValues.AbilityEnergy);
            result.SetAbilityDisplayMode("unit:60", "tech:25");
            result.SetAbilityDisplayMode("unit:63", MalcoPreferenceValues.AbilityEnergy);
            result.ShowTransportCargo = false;
            return result;
        }

        private static readonly string[] DefaultHiddenItemKeys =
        {
            "unit:64",
            "building:106", "building:107", "building:108", "building:109", "building:110",
            "building:125", "building:112", "building:122", "building:120", "building:123",
            "building:114", "building:115", "building:116", "building:117", "building:118",
            "building:142", "building:139", "building:135", "building:141", "building:137",
            "building:138", "building:136", "building:140", "building:144", "building:134",
            "building:154", "building:156", "building:157", "building:166", "building:164",
            "building:163", "building:165", "building:155", "building:171", "building:159",
            "building:169", "building:170", "building:172"
        };

        private static readonly string[] DefaultDisabledAvailableAlertKeys =
        {
            "upgrade:24", "upgrade:14", "upgrade:6", "tech:20", "upgrade:40", "tech:27",
            "upgrade:49", "tech:31", "upgrade:35", "upgrade:36", "upgrade:38", "upgrade:37",
            "upgrade:39", "upgrade:41", "upgrade:42", "tech:25", "upgrade:47", "upgrade:44",
            "upgrade:25", "upgrade:26", "tech:11", "upgrade:30", "upgrade:29", "tech:32",
            "upgrade:4", "upgrade:12", "tech:13", "tech:17", "upgrade:31", "upgrade:32",
            "tech:24", "tech:30", "upgrade:51", "tech:5", "tech:3", "upgrade:17",
            "upgrade:9", "upgrade:2", "upgrade:54", "tech:9", "upgrade:22", "tech:2",
            "upgrade:19", "tech:1", "upgrade:20", "upgrade:21", "tech:10", "tech:8"
        };

        private static readonly string[] DefaultDisabledCompletionAlertKeys =
        {
            "upgrade:24", "upgrade:14", "upgrade:6", "tech:20", "upgrade:40", "tech:27",
            "upgrade:49", "tech:31", "upgrade:35", "upgrade:36", "upgrade:38", "upgrade:37",
            "upgrade:39", "upgrade:41", "upgrade:42", "tech:25", "upgrade:47", "upgrade:44",
            "upgrade:25", "upgrade:26", "tech:11", "upgrade:30", "upgrade:29", "upgrade:4",
            "upgrade:12", "tech:13", "tech:17", "upgrade:31", "upgrade:32", "tech:24",
            "tech:30", "upgrade:51", "tech:5", "upgrade:17", "upgrade:9", "upgrade:2",
            "upgrade:54", "tech:9", "upgrade:22", "tech:2", "upgrade:19", "tech:1",
            "upgrade:20", "upgrade:21", "tech:10", "tech:8", "upgrade:16", "upgrade:27",
            "upgrade:28", "tech:15"
        };

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
