using System;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Malco.Configuration.Models;
using Malco.Data;
using Malco.Localization;
using Malco.Models;
using Malco.Settings.Contracts;

namespace Malco.Settings.Views
{
    internal sealed partial class FeatureSettingsView
    {
        private FrameworkElement BuildAbilityStatusSettings()
        {
            var host = new StackPanel { Margin = new Thickness(0d, 20d, 0d, 0d) };
            host.Children.Add(BuildFilterGroup(UiText.Get("Race"), BuildRaceSelector()));
            var rows = new StackPanel { Margin = new Thickness(0d, 14d, 0d, 0d) };
            foreach (var definition in AbilityCatalog.ForRace(_actions.SelectedTechTreeRace))
                rows.Children.Add(BuildAbilityStatusRow(definition));
            host.Children.Add(rows);
            return host;
        }

        private FrameworkElement BuildAbilityStatusRow(SpellcasterDefinition definition)
        {
            var grid = new Grid { Margin = new Thickness(12d, 0d, 12d, 0d) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44d) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(_compactLayout ? 250d : 310d) });
                grid.Children.Add(_previewFactory.BuildCatalogIcon(
                    TechTreeItem.Unit(definition.UnitId),
                    32d));
            var name = _palette.Text(UiText.GameName(definition.Name), 13d, FontWeights.SemiBold, _palette.TextBrush);
            name.VerticalAlignment = VerticalAlignment.Center;
            name.TextTrimming = TextTrimming.CharacterEllipsis;
            Grid.SetColumn(name, 1);
            grid.Children.Add(name);

            var layout = _actions.LayoutSnapshot;
            var abilityKey = "unit:" + definition.UnitId.ToString(CultureInfo.InvariantCulture);
            string configuredMode;
            var current = layout.AbilityDisplayModes.TryGetValue(abilityKey, out configuredMode)
                ? MalcoPreferenceValues.NormalizeAbilityDisplayModeForUnit(definition.UnitId, configuredMode)
                : MalcoPreferenceValues.AbilityHidden;
            var selector = new WrapPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center
            };
            var key = "unit:" + definition.UnitId.ToString(CultureInfo.InvariantCulture);
            selector.Children.Add(BuildAbilityModeTile(
                key,
                UiText.GameName(definition.Name),
                MalcoPreferenceValues.AbilityHidden,
                UiText.Get("Hidden"),
                current,
                null,
                "×"));
            selector.Children.Add(BuildAbilityModeTile(
                key,
                UiText.GameName(definition.Name),
                MalcoPreferenceValues.AbilityEnergy,
                UiText.Get("Energy number"),
                current,
                null,
                "MP"));
            foreach (var ability in definition.Abilities)
            {
                var icon = _previewFactory.ResolveUpgradeIcon(
                    new UpgradeState
                {
                    StateKey = ability.Mode,
                    Name = "Tech " + BwapiBroodWarTables.GetTechTypeName(ability.TechId)
                });
                selector.Children.Add(BuildAbilityModeTile(
                    key,
                    UiText.GameName(definition.Name),
                    ability.Mode,
                    UiText.GameName(ability.Name),
                    current,
                    icon,
                    string.IsNullOrWhiteSpace(ability.Name) ? "?" : ability.Name.Substring(0, 1)));
            }
            AutomationProperties.SetName(selector, UiText.GameName(definition.Name) + ": " + UiText.Get("Ability indicator"));
            Grid.SetColumn(selector, 2);
            grid.Children.Add(selector);
            return new Border
            {
                MinHeight = 58d,
                Background = Brushes.Transparent,
                BorderBrush = _palette.SeparatorBrush,
                BorderThickness = new Thickness(0d, 0d, 0d, 1d),
                Child = grid
            };
        }

        private Button BuildAbilityModeTile(
            string unitKey,
            string unitName,
            string mode,
            string label,
            string currentMode,
            ImageSource icon,
            string fallback)
        {
            var selected = string.Equals(mode, currentMode, StringComparison.Ordinal);
            var content = new Grid { Width = 38d, Height = 38d };
            UIElement tileVisual;
            if (icon != null)
            {
                tileVisual = new Image { Source = icon, Stretch = Stretch.Uniform, Margin = new Thickness(3d) };
            }
            else
            {
                var fallbackText = _palette.Text(fallback, fallback.Length > 1 ? 10d : 18d, FontWeights.Bold,
                    selected ? _palette.AccentBrush : _palette.TextBrush);
                fallbackText.HorizontalAlignment = HorizontalAlignment.Center;
                fallbackText.VerticalAlignment = VerticalAlignment.Center;
                fallbackText.TextAlignment = TextAlignment.Center;
                tileVisual = fallbackText;
            }
            content.Children.Add(new Border
            {
                Background = selected ? _palette.SelectedSurfaceBrush : _palette.RaisedSurfaceBrush,
                BorderBrush = selected ? _palette.AccentBrush : _palette.BorderBrush,
                BorderThickness = new Thickness(selected ? 2d : 1d),
                CornerRadius = new CornerRadius(4d),
                Child = tileVisual
            });
            if (selected)
            {
                var marker = new System.Windows.Shapes.Ellipse
                {
                    Width = 7d,
                    Height = 7d,
                    Fill = _palette.AccentBrush,
                    Stroke = _palette.InkBrush,
                    StrokeThickness = 1d,
                    Margin = new Thickness(0d, 2d, 2d, 0d),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Top
                };
                content.Children.Add(marker);
            }

            var automationName = unitName + ", " + label + ": " + UiText.Get(selected ? "Selected" : "Not selected");
            var button = new Button
            {
                Width = 44d,
                Height = 44d,
                Margin = new Thickness(0d, 0d, 5d, 0d),
                Padding = new Thickness(0d),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0d),
                ToolTip = label,
                Content = content
            };
            AutomationProperties.SetName(button, automationName);
            button.Click += (sender, args) =>
            {
                if (!_actions.ApplyEdit(SettingsEdit.SetAbilityDisplayMode(unitKey, mode)).Changed)
                {
                    return;
                }
                _actions.RefreshPresenterViews();
                _actions.RefreshSpatialPresentation();
                _actions.RefreshVisibility();
                RenderDetail();
                RestoreFocusAfterRender(unitName + ", " + label + ": " + UiText.Get("Selected"));
            };
            return button;
        }
    }
}
