using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Malco.Data;
using Malco.Localization;
using Malco.Models;
using Malco.Settings.Contracts;

namespace Malco.Settings.Views
{
    internal sealed partial class FeatureSettingsView
    {
        private FrameworkElement BuildBurrowedUnitsSettings()
        {
            var layout = _actions.LayoutSnapshot;
            var settings = new StackPanel { Margin = new Thickness(0d, 20d, 0d, 0d) };
            settings.Children.Add(BuildSwitchRow(
                _previewFactory.BuildFeatureIcon(FeatureSettingsPreviewKind.BurrowedUnits, 40d),
                UiText.Get("Burrowed unit icons"),
                UiText.Get("Show icons on burrowed Zerg units. Enemy units are shown only while detected. Units stacked on the same spot show a count."),
                layout.ShowBurrowedUnits,
                next => SettingsEdit.SetBurrowedUnitsVisible(next),
                true));
            settings.Children.Add(BuildSectionTitle(UiText.Get("Zerg")));
            var first = true;
            foreach (var unitId in BurrowableUnitCatalog.UnitIds)
            {
                var id = unitId;
                var item = TechTreeItem.Unit(id);
                settings.Children.Add(BuildSwitchRow(
                    _previewFactory.BuildCatalogIcon(item, 32d),
                    UiText.GameName(item.Name),
                    null,
                    !layout.HiddenBurrowedUnitIds.Contains(id),
                    next => SettingsEdit.SetBurrowedUnitVisible(id, next),
                    first));
                first = false;
            }
            return settings;
        }
    }
}
