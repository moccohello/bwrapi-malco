using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using Malco.Configuration.Models;

namespace Malco.Configuration
{
    internal sealed partial class HudLayoutFileStore
    {
        private static bool TryRead(
            string path,
            out HudLayoutConfig layout,
            out bool newerSchema,
            out bool unsupportedSchema)
        {
            layout = null;
            newerSchema = false;
            unsupportedSchema = false;
            try
            {
                var json = File.ReadAllText(path);
                using (var document = JsonDocument.Parse(json))
                {
                    int schemaVersion;
                    bool schemaPropertyPresent;
                    var hasSchema = TryGetSchemaVersion(
                        document.RootElement,
                        out schemaVersion,
                        out schemaPropertyPresent);
                    if (schemaPropertyPresent && !hasSchema)
                    {
                        return false;
                    }
                    if (!hasSchema || schemaVersion != CurrentSchemaVersion)
                    {
                        unsupportedSchema = true;
                        newerSchema = hasSchema && schemaVersion > CurrentSchemaVersion;
                        return false;
                    }
                }

                layout = JsonSerializer.Deserialize<HudLayoutConfig>(json, SerializerOptions);
                if (layout == null)
                {
                    return false;
                }

                NormalizeCurrentSchema(layout);
                layout.SchemaVersion = CurrentSchemaVersion;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryGetSchemaVersion(
            JsonElement root,
            out int schemaVersion,
            out bool propertyPresent)
        {
            schemaVersion = 0;
            propertyPresent = false;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            foreach (var property in root.EnumerateObject())
            {
                if (string.Equals(property.Name, "SchemaVersion", StringComparison.OrdinalIgnoreCase))
                {
                    propertyPresent = true;
                    return property.Value.ValueKind == JsonValueKind.Number &&
                           property.Value.TryGetInt32(out schemaVersion);
                }
            }

            return false;
        }

        private static void NormalizeCurrentSchema(
            HudLayoutConfig layout)
        {
            var normalizedLanguage = MalcoPreferenceValues.NormalizeLanguage(layout.Language);
            layout.Language = normalizedLanguage;

            var normalizedCompletionMode = MalcoPreferenceValues.NormalizeCompletionMode(layout.CompletionDisplayMode);
            layout.CompletionDisplayMode = normalizedCompletionMode;
            var normalizedCountdownSeconds =
                MalcoPreferenceValues.NormalizeCompletionCountdownSeconds(layout.CompletionCountdownSeconds);
            layout.CompletionCountdownSeconds = normalizedCountdownSeconds;
            var workerCountStyle = MalcoPreferenceValues.NormalizeWorkerCountStyle(layout.WorkerCountStyle);
            layout.WorkerCountStyle = workerCountStyle;
            var unitIconSize = MalcoPreferenceValues.NormalizeIconSize(
                layout.UnitIconSize,
                MalcoPreferenceValues.IconLarge);
            layout.UnitIconSize = unitIconSize;
            var buildingIconSize = MalcoPreferenceValues.NormalizeIconSize(
                layout.BuildingIconSize,
                MalcoPreferenceValues.IconLarge);
            layout.BuildingIconSize = buildingIconSize;
            var completedUpgradeIconSize = MalcoPreferenceValues.NormalizeIconSize(
                layout.CompletedUpgradeIconSize,
                MalcoPreferenceValues.IconMedium);
            layout.CompletedUpgradeIconSize = completedUpgradeIconSize;
            var availableUpgradeIconSize = MalcoPreferenceValues.NormalizeIconSize(
                layout.AvailableUpgradeIconSize,
                MalcoPreferenceValues.IconSmall);
            layout.AvailableUpgradeIconSize = availableUpgradeIconSize;
            if (layout.Widgets == null)
            {
                layout.Widgets = new Dictionary<string, WidgetLayout>(StringComparer.OrdinalIgnoreCase);
            }
            else if (!layout.Widgets.Comparer.Equals(StringComparer.OrdinalIgnoreCase))
            {
                layout.Widgets = new Dictionary<string, WidgetLayout>(layout.Widgets, StringComparer.OrdinalIgnoreCase);
            }
            foreach (var definition in HudWidgetRegistry.EditorFeatures())
            {
                WidgetLayout widget;
                if (!layout.Widgets.TryGetValue(definition.Key, out widget) || widget == null)
                {
                    layout.GetOrCreate(
                        definition.Key,
                        definition.X,
                        definition.Y,
                        definition.Width,
                        definition.Height,
                        definition.EnabledByDefault);
                }
            }

            if (layout.ItemSettings == null)
            {
                layout.ItemSettings = new Dictionary<string, HudItemSetting>(StringComparer.OrdinalIgnoreCase);
            }
            else if (!layout.ItemSettings.Comparer.Equals(StringComparer.OrdinalIgnoreCase))
            {
                layout.ItemSettings = new Dictionary<string, HudItemSetting>(layout.ItemSettings, StringComparer.OrdinalIgnoreCase);
            }
            var normalizedAbilityModes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in layout.AbilityDisplayModes ?? new Dictionary<string, string>())
            {
                int unitId;
                if (string.IsNullOrWhiteSpace(entry.Key) ||
                    !entry.Key.StartsWith("unit:", StringComparison.OrdinalIgnoreCase) ||
                    !int.TryParse(entry.Key.Substring(5), NumberStyles.Integer, CultureInfo.InvariantCulture, out unitId))
                {
                    continue;
                }

                var key = "unit:" + unitId.ToString(CultureInfo.InvariantCulture);
                var mode = MalcoPreferenceValues.NormalizeAbilityDisplayModeForUnit(unitId, entry.Value);
                normalizedAbilityModes[key] = mode;
            }
            layout.AbilityDisplayModes = normalizedAbilityModes;

            EnforceMinimum(layout, HudWidgetRegistry.Upgrades);
            EnforceMinimum(layout, HudWidgetRegistry.AvailableUpgrades);
            EnforceMinimum(layout, HudWidgetRegistry.UpgradeCompletionWarnings);
        }

        private static void EnforceMinimum(HudLayoutConfig layout, string key)
        {
            WidgetLayout widget;
            if (!layout.Widgets.TryGetValue(key, out widget) || widget == null)
            {
                return;
            }

            var width = Math.Max(widget.Width, HudWidgetLayoutPolicy.MinimumWidth(key));
            var height = Math.Max(widget.Height, HudWidgetLayoutPolicy.MinimumHeight(key));
            widget.Width = width;
            widget.Height = height;
        }
    }
}
