using System.Collections.Generic;
using System.Linq;
using Malco.Configuration.Models;

namespace Malco
{
    internal sealed class HudWidgetDefinition
    {
        public HudWidgetDefinition(
            string key,
            string title,
            string editorLabel,
            string description,
            double x,
            double y,
            double width,
            double height,
            bool enabledByDefault = true)
        {
            Key = key;
            Title = title;
            EditorLabel = editorLabel;
            Description = description;
            X = x;
            Y = y;
            Width = width;
            Height = height;
            EnabledByDefault = enabledByDefault;
        }

        public string Key { get; private set; }

        public string Title { get; private set; }

        public string EditorLabel { get; private set; }

        public string Description { get; private set; }

        public double X { get; private set; }

        public double Y { get; private set; }

        public double Width { get; private set; }

        public double Height { get; private set; }

        public bool EnabledByDefault { get; private set; }
    }

    internal static class HudWidgetRegistry
    {
        public const string Workers = "workers";
        public const string Units = "units";
        public const string Buildings = "buildings";
        public const string Upgrades = "upgrades";
        public const string AvailableUpgrades = "available-upgrades";
        public const string UpgradeCompletionWarnings = "upgrade-completion-warnings";
        public const string BuildingRallyLines = "building-rally-lines";
        public const string UnitCommandLines = "unit-command-lines";
        public const string MineralWorkers = "mineral-workers";
        public const string GasWorkers = "gas-workers";

        public static readonly HudWidgetDefinition WorkersWidget =
            new HudWidgetDefinition(Workers, "Workers", "Worker count", "Movable and resizable idle and total worker counts.", 1130d, 31.305d, 142d, 32d);

        public static readonly HudWidgetDefinition UnitsWidget =
            new HudWidgetDefinition(Units, "Units", "Units", "Combat-unit icons with current counts.", 4d, 4d, 198.50733476318817d, 256.6130825412207d, false);

        public static readonly HudWidgetDefinition BuildingsWidget =
            new HudWidgetDefinition(Buildings, "Buildings", "Buildings", "Owned building icons with current counts.", 230.91536500680205d, 4d, 197.68646706487567d, 257.39085061771215d, false);

        public static readonly HudWidgetDefinition UpgradesWidget =
            new HudWidgetDefinition(Upgrades, "Completed upgrades and research", "Completed upgrades and research", "Completed upgrades, levels, and researched abilities.", 446.7816543476067d, 4d, 166.5d, 169d, false);

        public static readonly HudWidgetDefinition AvailableUpgradesWidget =
            new HudWidgetDefinition(AvailableUpgrades, "Research available", "Research available", "Upgrades and abilities you can start now; blocked options are dimmed.", 1111.5378501135505d, 403.93871584178314d, 164.46214988644965d, 87.67757742999326d);

        public static readonly HudWidgetDefinition UpgradeCompletionWarningsWidget =
            new HudWidgetDefinition(UpgradeCompletionWarnings, "Upcoming research completion", "Upcoming research completion", "A configurable countdown or full progress for upgrades and abilities.", 1104.4447388342163d, 86.62266113943417d, 171.5552611657835d, 210.6680545041635d);

        public static IEnumerable<HudWidgetDefinition> EditorFeatures()
        {
            yield return WorkersWidget;
            yield return UnitsWidget;
            yield return BuildingsWidget;
            yield return UpgradesWidget;
            yield return AvailableUpgradesWidget;
            yield return UpgradeCompletionWarningsWidget;
            yield return new HudWidgetDefinition(BuildingRallyLines, "Building rally lines", "Building rally lines", "Rally paths for selected production buildings.", 0d, 0d, 1d, 1d);
            yield return new HudWidgetDefinition(UnitCommandLines, "Unit command lines", "Unit command lines", "Current and queued command paths for selected units, plus active targets for selected defensive buildings.", 0d, 0d, 1d, 1d, false);
            yield return new HudWidgetDefinition(MineralWorkers, "Minerals", "Minerals", "Mineral worker counts by base.", 0d, 0d, 1d, 1d);
            yield return new HudWidgetDefinition(GasWorkers, "Gas", "Gas", "Gas worker counts by refinery.", 0d, 0d, 1d, 1d);
        }

        public static bool IsSpatialFeature(string key)
        {
            return string.Equals(key, BuildingRallyLines, System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(key, UnitCommandLines, System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(key, MineralWorkers, System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(key, GasWorkers, System.StringComparison.OrdinalIgnoreCase);
        }

        public static HudWidgetDefinition Find(string key) => EditorFeatures().FirstOrDefault(
            feature => string.Equals(feature.Key, key, System.StringComparison.OrdinalIgnoreCase));

        public static string GetIconSize(HudLayoutSnapshot layout, string key)
        {
            if (string.Equals(key, Units, System.StringComparison.OrdinalIgnoreCase)) return layout.UnitIconSize;
            if (string.Equals(key, Buildings, System.StringComparison.OrdinalIgnoreCase)) return layout.BuildingIconSize;
            if (string.Equals(key, Upgrades, System.StringComparison.OrdinalIgnoreCase)) return layout.CompletedUpgradeIconSize;
            return layout.AvailableUpgradeIconSize;
        }
    }

    internal static class HudWidgetLayoutPolicy
    {
        private const double MinimumWidgetWidth = 64d;
        private const double MinimumWidgetHeight = 48d;

        public static double MinimumWidth(string key)
        {
            if (string.Equals(key, HudWidgetRegistry.UpgradeCompletionWarnings, System.StringComparison.OrdinalIgnoreCase))
            {
                return 144d;
            }

            return MinimumWidgetWidth;
        }

        public static double MinimumHeight(string key)
        {
            if (string.Equals(key, HudWidgetRegistry.Workers, System.StringComparison.OrdinalIgnoreCase))
            {
                return 28d;
            }

            if (string.Equals(key, HudWidgetRegistry.UpgradeCompletionWarnings, System.StringComparison.OrdinalIgnoreCase))
            {
                return 48d;
            }

            return MinimumWidgetHeight;
        }
    }
}
