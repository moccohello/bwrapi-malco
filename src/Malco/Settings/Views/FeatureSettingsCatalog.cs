using System;
using System.Collections.Generic;
using System.Linq;
using Malco.Localization;

namespace Malco.Settings.Views
{
    internal enum FeatureSettingsGroup
    {
        Status,
        Alerts,
        Guides,
        General
    }

    internal enum FeatureSettingsDetailKind
    {
        Standard,
        ResourceWorkers,
        TransportCargo,
        AbilityStatus,
        General
    }

    internal enum FeatureSettingsPreviewKind
    {
        Buildings,
        Units,
        Workers,
        Completed,
        Available,
        Progress,
        Command,
        Rally,
        Resources,
        Transport,
        Ability,
        General,
        Mineral,
        Gas
    }

    internal sealed class FeatureSettingsDefinition
    {
        public FeatureSettingsDefinition(
            string key,
            string titleKey,
            string descriptionKey,
            FeatureSettingsPreviewKind previewKind,
            FeatureSettingsGroup group,
            FeatureSettingsDetailKind detailKind,
            FeatureItemPolicy itemPolicy,
            bool supportsIconSize,
            bool isSpatial)
        {
            Key = key;
            TitleKey = titleKey;
            DescriptionKey = descriptionKey;
            PreviewKind = previewKind;
            Group = group;
            DetailKind = detailKind;
            ItemPolicy = itemPolicy;
            SupportsIconSize = supportsIconSize;
            IsSpatial = isSpatial;
        }

        public string Key { get; }

        public string TitleKey { get; }

        public string DescriptionKey { get; }

        public string Title => UiText.Get(TitleKey);

        public string Description => UiText.Get(DescriptionKey);

        public FeatureSettingsPreviewKind PreviewKind { get; }

        public FeatureSettingsGroup Group { get; }

        public FeatureSettingsDetailKind DetailKind { get; }

        public FeatureItemPolicy ItemPolicy { get; }

        public bool SupportsIconSize { get; }

        public bool IsSpatial { get; }
    }

    internal static class HudFeatureCatalog
    {
        public const string GeneralKey = "general";
        public const string ResourceWorkersKey = "resource-workers";
        public const string TransportCargoKey = "transport-cargo";
        public const string AbilityStatusKey = "ability-status";

        private static readonly FeatureItemPolicy UnitsPolicy = new FeatureItemPolicy(
            FeatureItemSourceKind.Units,
            FeatureItemSettingKind.Shown);
        private static readonly FeatureItemPolicy BuildingsPolicy = new FeatureItemPolicy(
            FeatureItemSourceKind.Buildings,
            FeatureItemSettingKind.Shown);
        private static readonly FeatureItemPolicy ResearchPolicy = new FeatureItemPolicy(
            FeatureItemSourceKind.Research,
            FeatureItemSettingKind.Shown);
        private static readonly FeatureItemPolicy AvailablePolicy = new FeatureItemPolicy(
            FeatureItemSourceKind.Research,
            FeatureItemSettingKind.AvailableAlert);
        private static readonly FeatureItemPolicy CompletionPolicy = new FeatureItemPolicy(
            FeatureItemSourceKind.Research,
            FeatureItemSettingKind.CompletionAlert);

        private static readonly IReadOnlyList<FeatureSettingsDefinition> FeatureDefinitions =
            Array.AsReadOnly(new[]
            {
                new FeatureSettingsDefinition(
                    HudWidgetRegistry.Buildings,
                    "Building counts",
                    "Owned building icons with current counts.",
                    FeatureSettingsPreviewKind.Buildings,
                    FeatureSettingsGroup.Status,
                    FeatureSettingsDetailKind.Standard,
                    BuildingsPolicy,
                    true,
                    false),
                new FeatureSettingsDefinition(
                    HudWidgetRegistry.Units,
                    "Unit counts",
                    "Combat-unit icons with current counts.",
                    FeatureSettingsPreviewKind.Units,
                    FeatureSettingsGroup.Status,
                    FeatureSettingsDetailKind.Standard,
                    UnitsPolicy,
                    true,
                    false),
                new FeatureSettingsDefinition(
                    HudWidgetRegistry.Workers,
                    "Worker count",
                    "Idle and total worker counts.",
                    FeatureSettingsPreviewKind.Workers,
                    FeatureSettingsGroup.Status,
                    FeatureSettingsDetailKind.Standard,
                    default,
                    false,
                    false),
                new FeatureSettingsDefinition(
                    HudWidgetRegistry.Upgrades,
                    "Completed upgrades",
                    "Completed upgrades, levels, and researched abilities.",
                    FeatureSettingsPreviewKind.Completed,
                    FeatureSettingsGroup.Status,
                    FeatureSettingsDetailKind.Standard,
                    ResearchPolicy,
                    true,
                    false),
                new FeatureSettingsDefinition(
                    HudWidgetRegistry.AvailableUpgrades,
                    "Research available",
                    "Upgrades and abilities you can start now.",
                    FeatureSettingsPreviewKind.Available,
                    FeatureSettingsGroup.Alerts,
                    FeatureSettingsDetailKind.Standard,
                    AvailablePolicy,
                    true,
                    false),
                new FeatureSettingsDefinition(
                    HudWidgetRegistry.UpgradeCompletionWarnings,
                    "Upcoming research completion",
                    "Show a configurable countdown or full progress.",
                    FeatureSettingsPreviewKind.Progress,
                    FeatureSettingsGroup.Alerts,
                    FeatureSettingsDetailKind.Standard,
                    CompletionPolicy,
                    false,
                    false),
                new FeatureSettingsDefinition(
                    HudWidgetRegistry.UnitCommandLines,
                    "Command paths",
                    "Current and queued command paths for selected units.",
                    FeatureSettingsPreviewKind.Command,
                    FeatureSettingsGroup.Guides,
                    FeatureSettingsDetailKind.Standard,
                    default,
                    false,
                    true),
                new FeatureSettingsDefinition(
                    HudWidgetRegistry.BuildingRallyLines,
                    "Rally paths",
                    "Rally paths for selected production buildings.",
                    FeatureSettingsPreviewKind.Rally,
                    FeatureSettingsGroup.Guides,
                    FeatureSettingsDetailKind.Standard,
                    default,
                    false,
                    true),
                new FeatureSettingsDefinition(
                    ResourceWorkersKey,
                    "Resource worker indicators",
                    "Choose which resource worker counts appear on the map.",
                    FeatureSettingsPreviewKind.Resources,
                    FeatureSettingsGroup.Guides,
                    FeatureSettingsDetailKind.ResourceWorkers,
                    default,
                    false,
                    true),
                new FeatureSettingsDefinition(
                    TransportCargoKey,
                    "Transport cargo display",
                    "Show or hide passenger icons and counts inside loaded transports.",
                    FeatureSettingsPreviewKind.Transport,
                    FeatureSettingsGroup.Guides,
                    FeatureSettingsDetailKind.TransportCargo,
                    default,
                    false,
                    true),
                new FeatureSettingsDefinition(
                    AbilityStatusKey,
                    "Ability readiness display",
                    "Choose energy or a ready-skill dot for each ability-capable unit or building.",
                    FeatureSettingsPreviewKind.Ability,
                    FeatureSettingsGroup.Guides,
                    FeatureSettingsDetailKind.AbilityStatus,
                    default,
                    false,
                    true)
            });

        private static readonly FeatureSettingsDefinition GeneralDefinition =
            new FeatureSettingsDefinition(
                GeneralKey,
                "General",
                "Language and app-wide preferences.",
                FeatureSettingsPreviewKind.General,
                FeatureSettingsGroup.General,
                FeatureSettingsDetailKind.General,
                default,
                false,
                false);

        public static FeatureSettingsDefinition General => GeneralDefinition;

        public static FeatureSettingsDefinition FirstFeature => FeatureDefinitions[0];

        public static IEnumerable<FeatureSettingsDefinition> InGroup(FeatureSettingsGroup group) =>
            FeatureDefinitions.Where(feature => feature.Group == group);

        public static FeatureSettingsDefinition Find(string key)
        {
            if (string.Equals(key, GeneralKey, StringComparison.OrdinalIgnoreCase))
            {
                return GeneralDefinition;
            }

            return FeatureDefinitions.FirstOrDefault(feature =>
                string.Equals(feature.Key, key, StringComparison.OrdinalIgnoreCase));
        }

        public static string NormalizeSelectionKey(string key)
        {
            return string.Equals(key, HudWidgetRegistry.MineralWorkers, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(key, HudWidgetRegistry.GasWorkers, StringComparison.OrdinalIgnoreCase)
                ? ResourceWorkersKey
                : key;
        }

        public static bool IsGeneral(string key) =>
            string.Equals(key, GeneralKey, StringComparison.OrdinalIgnoreCase);

        public static bool IsSpatial(string key)
        {
            var definition = Find(key);
            return definition?.IsSpatial == true || HudWidgetRegistry.IsSpatialFeature(key);
        }
    }
}
