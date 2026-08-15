using System;
using System.Collections.Generic;
using System.Linq;
using Malco.Configuration.Models;
using Malco.Data;
using Malco.Localization;
using Malco.Settings.Contracts;

namespace Malco.Settings.Views
{
    internal enum FeatureItemSourceKind
    {
        None,
        Units,
        Buildings,
        Research
    }

    internal enum FeatureItemSettingKind
    {
        None,
        Shown,
        AvailableAlert,
        CompletionAlert
    }

    internal readonly struct FeatureItemPolicy
    {
        public FeatureItemPolicy(
            FeatureItemSourceKind sourceKind,
            FeatureItemSettingKind settingKind)
        {
            SourceKind = sourceKind;
            SettingKind = settingKind;
        }

        public FeatureItemSourceKind SourceKind { get; }

        public FeatureItemSettingKind SettingKind { get; }

        public bool HasItems =>
            SourceKind != FeatureItemSourceKind.None &&
            SettingKind != FeatureItemSettingKind.None;

        public IEnumerable<TechTreeItem> SelectItems(TechTreeRaceCatalog catalog)
        {
            IEnumerable<TechTreeItem> items;
            switch (SourceKind)
            {
                case FeatureItemSourceKind.Units:
                    items = catalog.Branches
                        .SelectMany(branch => branch.Items)
                        .Where(item =>
                            item.Kind == TechTreeItemKind.Unit &&
                            !BwapiBroodWarTables.IsWorkerUnitId(item.UnitId));
                    break;
                case FeatureItemSourceKind.Buildings:
                    items = catalog.Branches.Select(branch => branch.Building);
                    break;
                case FeatureItemSourceKind.Research:
                    items = catalog.Branches
                        .SelectMany(branch => branch.Items)
                        .Where(item =>
                            item.Kind == TechTreeItemKind.Upgrade ||
                            (item.Kind == TechTreeItemKind.Tech &&
                             UpgradeAvailabilityPolicy.IsResearchTechTypeId(item.TechIndex)));
                    break;
                default:
                    return Enumerable.Empty<TechTreeItem>();
            }

            return items
                .GroupBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First());
        }

        public bool ReadValue(HudLayoutSnapshot layout, TechTreeItem item)
        {
            HudItemSettingSnapshot setting = default;
            var hasSetting = layout != null &&
                             layout.ItemSettings.TryGetValue(item.Key, out setting);
            switch (SettingKind)
            {
                case FeatureItemSettingKind.AvailableAlert:
                    return !hasSetting || setting.AvailableAlert;
                case FeatureItemSettingKind.CompletionAlert:
                    return !hasSetting || setting.CompletionAlert;
                case FeatureItemSettingKind.Shown:
                    return !hasSetting || setting.Show;
                default:
                    throw new InvalidOperationException("The feature has no item setting policy.");
            }
        }

        public SettingsEdit CreateItemDelta(TechTreeItem item, bool enabled)
        {
            switch (SettingKind)
            {
                case FeatureItemSettingKind.AvailableAlert:
                    return SettingsEdit.SetAvailableAlert(item.Key, enabled);
                case FeatureItemSettingKind.CompletionAlert:
                    return SettingsEdit.SetCompletionAlert(item.Key, enabled);
                case FeatureItemSettingKind.Shown:
                    return SettingsEdit.SetItemShown(item.Key, enabled);
                default:
                    throw new InvalidOperationException("The feature has no item setting policy.");
            }
        }

        public SettingsEdit CreateBulkDelta(
            IEnumerable<TechTreeItem> items,
            bool enabled)
        {
            var keys = (items ?? Enumerable.Empty<TechTreeItem>())
                .Where(item => item != null)
                .Select(item => item.Key)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            switch (SettingKind)
            {
                case FeatureItemSettingKind.AvailableAlert:
                    return SettingsEdit.SetAvailableAlerts(keys, enabled);
                case FeatureItemSettingKind.CompletionAlert:
                    return SettingsEdit.SetCompletionAlerts(keys, enabled);
                case FeatureItemSettingKind.Shown:
                    return SettingsEdit.SetItemsShown(keys, enabled);
                default:
                    throw new InvalidOperationException("The feature has no item setting policy.");
            }
        }

        public string SettingLabel
        {
            get
            {
                switch (SettingKind)
                {
                    case FeatureItemSettingKind.AvailableAlert:
                        return UiText.Get("Alert when available");
                    case FeatureItemSettingKind.CompletionAlert:
                        return UiText.Get("Warn before completion");
                    default:
                        return UiText.Get("Show in HUD");
                }
            }
        }

        public string SectionTitle
        {
            get
            {
                switch (SettingKind)
                {
                    case FeatureItemSettingKind.AvailableAlert:
                        return UiText.Get("Availability alerts");
                    case FeatureItemSettingKind.CompletionAlert:
                        return UiText.Get("Completion alerts");
                    default:
                        return UiText.Get("Displayed content");
                }
            }
        }
    }
}
