using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Malco.Configuration.Models
{
    internal readonly struct WidgetLayoutSnapshot
    {
        public WidgetLayoutSnapshot(WidgetLayout source)
        {
            Enabled = source != null && source.Enabled;
            X = source != null ? source.X : 0d;
            Y = source != null ? source.Y : 0d;
            Width = source != null ? source.Width : 0d;
            Height = source != null ? source.Height : 0d;
            HasRelativeBounds = source != null && source.HasRelativeBounds;
            XRatio = source != null ? source.XRatio : 0d;
            YRatio = source != null ? source.YRatio : 0d;
            WidthRatio = source != null ? source.WidthRatio : 0d;
            HeightRatio = source != null ? source.HeightRatio : 0d;
        }

        public bool Enabled { get; }
        public double X { get; }
        public double Y { get; }
        public double Width { get; }
        public double Height { get; }
        public bool HasRelativeBounds { get; }
        public double XRatio { get; }
        public double YRatio { get; }
        public double WidthRatio { get; }
        public double HeightRatio { get; }

        internal WidgetLayout ToMutable()
        {
            return new WidgetLayout
            {
                Enabled = Enabled,
                X = X,
                Y = Y,
                Width = Width,
                Height = Height,
                HasRelativeBounds = HasRelativeBounds,
                XRatio = XRatio,
                YRatio = YRatio,
                WidthRatio = WidthRatio,
                HeightRatio = HeightRatio
            };
        }
    }

    internal readonly struct HudItemSettingSnapshot
    {
        public HudItemSettingSnapshot(HudItemSetting source)
        {
            Show = source == null || source.Show;
            AvailableAlert = source == null || source.AvailableAlert;
            CompletionAlert = source == null || source.CompletionAlert;
        }

        public bool Show { get; }
        public bool AvailableAlert { get; }
        public bool CompletionAlert { get; }

        internal HudItemSetting ToMutable()
        {
            return new HudItemSetting
            {
                Show = Show,
                AvailableAlert = AvailableAlert,
                CompletionAlert = CompletionAlert
            };
        }
    }

    internal sealed class HudLayoutSnapshot
    {
        private readonly ReadOnlyDictionary<string, WidgetLayoutSnapshot> _widgets;
        private readonly ReadOnlyDictionary<string, HudItemSettingSnapshot> _itemSettings;
        private readonly ReadOnlyDictionary<string, string> _abilityDisplayModes;

        private HudLayoutSnapshot(HudLayoutConfig source)
        {
            source = source ?? HudLayoutConfig.CreateDefault();
            SchemaVersion = source.SchemaVersion;
            Language = MalcoPreferenceValues.NormalizeLanguage(source.Language);
            CompletionDisplayMode = MalcoPreferenceValues.NormalizeCompletionMode(source.CompletionDisplayMode);
            CompletionCountdownSeconds = MalcoPreferenceValues.NormalizeCompletionCountdownSeconds(source.CompletionCountdownSeconds);
            UnitIconSize = source.GetIconSize(HudWidgetRegistry.Units);
            BuildingIconSize = source.GetIconSize(HudWidgetRegistry.Buildings);
            CompletedUpgradeIconSize = source.GetIconSize(HudWidgetRegistry.Upgrades);
            AvailableUpgradeIconSize = source.GetIconSize(HudWidgetRegistry.AvailableUpgrades);
            WorkerCountStyle = MalcoPreferenceValues.NormalizeWorkerCountStyle(source.WorkerCountStyle);
            ShowTransportCargo = source.ShowTransportCargo;
            _widgets = CopyWidgets(source.Widgets);
            _itemSettings = CopyItems(source.ItemSettings);
            _abilityDisplayModes = CopyStrings(source.AbilityDisplayModes);
        }

        public int SchemaVersion { get; }
        public string Language { get; }
        public string CompletionDisplayMode { get; }
        public int CompletionCountdownSeconds { get; }
        public string UnitIconSize { get; }
        public string BuildingIconSize { get; }
        public string CompletedUpgradeIconSize { get; }
        public string AvailableUpgradeIconSize { get; }
        public string WorkerCountStyle { get; }
        public bool ShowTransportCargo { get; }
        public IReadOnlyDictionary<string, WidgetLayoutSnapshot> Widgets { get { return _widgets; } }
        public IReadOnlyDictionary<string, HudItemSettingSnapshot> ItemSettings { get { return _itemSettings; } }
        public IReadOnlyDictionary<string, string> AbilityDisplayModes { get { return _abilityDisplayModes; } }

        public static HudLayoutSnapshot FromLayout(HudLayoutConfig source)
        {
            return new HudLayoutSnapshot(source);
        }

        public HudLayoutConfig ToMutable()
        {
            var result = new HudLayoutConfig
            {
                SchemaVersion = SchemaVersion,
                Language = Language,
                CompletionDisplayMode = CompletionDisplayMode,
                CompletionCountdownSeconds = CompletionCountdownSeconds,
                UnitIconSize = UnitIconSize,
                BuildingIconSize = BuildingIconSize,
                CompletedUpgradeIconSize = CompletedUpgradeIconSize,
                AvailableUpgradeIconSize = AvailableUpgradeIconSize,
                WorkerCountStyle = WorkerCountStyle,
                ShowTransportCargo = ShowTransportCargo
            };
            foreach (var entry in _widgets)
            {
                result.Widgets[entry.Key] = entry.Value.ToMutable();
            }

            foreach (var entry in _itemSettings)
            {
                result.ItemSettings[entry.Key] = entry.Value.ToMutable();
            }

            foreach (var entry in _abilityDisplayModes)
            {
                result.AbilityDisplayModes[entry.Key] = entry.Value;
            }

            return result;
        }

        private static ReadOnlyDictionary<string, WidgetLayoutSnapshot> CopyWidgets(
            IDictionary<string, WidgetLayout> source)
        {
            var copy = new Dictionary<string, WidgetLayoutSnapshot>(StringComparer.OrdinalIgnoreCase);
            if (source != null)
            {
                foreach (var entry in source)
                {
                    if (!string.IsNullOrEmpty(entry.Key) && entry.Value != null)
                    {
                        copy[entry.Key] = new WidgetLayoutSnapshot(entry.Value);
                    }
                }
            }

            return new ReadOnlyDictionary<string, WidgetLayoutSnapshot>(copy);
        }

        private static ReadOnlyDictionary<string, HudItemSettingSnapshot> CopyItems(
            IDictionary<string, HudItemSetting> source)
        {
            var copy = new Dictionary<string, HudItemSettingSnapshot>(StringComparer.OrdinalIgnoreCase);
            if (source != null)
            {
                foreach (var entry in source)
                {
                    if (!string.IsNullOrEmpty(entry.Key) && entry.Value != null)
                    {
                        copy[entry.Key] = new HudItemSettingSnapshot(entry.Value);
                    }
                }
            }

            return new ReadOnlyDictionary<string, HudItemSettingSnapshot>(copy);
        }

        private static ReadOnlyDictionary<string, string> CopyStrings(IDictionary<string, string> source)
        {
            var copy = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (source != null)
            {
                foreach (var entry in source)
                {
                    if (!string.IsNullOrEmpty(entry.Key))
                    {
                        int unitId;
                        copy[entry.Key] = entry.Key.StartsWith("unit:", StringComparison.OrdinalIgnoreCase) &&
                                          int.TryParse(entry.Key.Substring(5), out unitId)
                            ? MalcoPreferenceValues.NormalizeAbilityDisplayModeForUnit(unitId, entry.Value)
                            : MalcoPreferenceValues.AbilityHidden;
                    }
                }
            }
            return new ReadOnlyDictionary<string, string>(copy);
        }
    }
}
