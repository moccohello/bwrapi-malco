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
        private void RenderDetail()
        {
            _detailHost.Children.Clear();
            _itemListHost = null;
            _searchInput = null;
            var feature = HudFeatureCatalog.Find(_selectedFeatureKey);
            if (feature == null)
            {
                feature = HudFeatureCatalog.FirstFeature;
                _selectedFeatureKey = feature.Key;
            }

            if (feature.DetailKind == FeatureSettingsDetailKind.General)
            {
                _detailHost.Children.Add(BuildGeneralDetail());
                _detailScroll.ScrollToTop();
                return;
            }

            _detailHost.Children.Add(BuildFeatureHeader(
                feature,
                feature.DetailKind == FeatureSettingsDetailKind.Standard));
            if (feature.DetailKind == FeatureSettingsDetailKind.ResourceWorkers)
            {
                _detailHost.Children.Add(BuildResourceWorkerSettings());
                _detailScroll.ScrollToTop();
                return;
            }
            if (feature.DetailKind == FeatureSettingsDetailKind.TransportCargo)
            {
                _detailHost.Children.Add(BuildTransportCargoSettings());
                _detailScroll.ScrollToTop();
                return;
            }
            if (feature.DetailKind == FeatureSettingsDetailKind.AbilityStatus)
            {
                _detailHost.Children.Add(BuildAbilityStatusSettings());
                _detailScroll.ScrollToTop();
                return;
            }
            if (string.Equals(feature.Key, HudWidgetRegistry.Workers, StringComparison.OrdinalIgnoreCase))
            {
                _detailHost.Children.Add(BuildWorkerCountStyleSelector());
            }
            if (feature.SupportsIconSize)
            {
                _detailHost.Children.Add(BuildIconSizeSelector(feature.Key));
            }
            if (feature.ItemPolicy.SettingKind == FeatureItemSettingKind.CompletionAlert)
            {
                _detailHost.Children.Add(BuildCompletionModeSelector());
                if (string.Equals(
                        MalcoPreferenceValues.NormalizeCompletionMode(_actions.Layout.CompletionDisplayMode),
                        MalcoPreferenceValues.Countdown10Seconds,
                        StringComparison.Ordinal))
                {
                    _detailHost.Children.Add(BuildCompletionCountdownSelector());
                }
            }
            if (feature.ItemPolicy.HasItems)
            {
                _detailHost.Children.Add(BuildFilterTools(feature));
                _itemListHost = new StackPanel();
                _detailHost.Children.Add(_itemListHost);
                RefreshItemList(feature);
            }
            _detailScroll.ScrollToTop();
        }

        private FrameworkElement BuildFeatureHeader(
            FeatureSettingsDefinition feature,
            bool showFeatureSwitch = true)
        {
            var grid = new Grid { Margin = new Thickness(16d) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(_compactLayout ? 0d : 94d) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            if (!_compactLayout)
            {
                var preview =
                    _previewFactory.BuildFeaturePreview(feature.PreviewKind);
                grid.Children.Add(preview);
            }
            var copy = new StackPanel
            {
                Margin = new Thickness(16d, 4d, 20d, 0d),
                VerticalAlignment = VerticalAlignment.Center
            };
            copy.Children.Add(_palette.Text(feature.Title, 20d, FontWeights.Bold, _palette.TextBrush));
            var description = _palette.Text(feature.Description, 13d, FontWeights.Normal, _palette.MutedBrush);
            description.TextWrapping = TextWrapping.Wrap;
            description.Margin = new Thickness(0d, 4d, 0d, 0d);
            copy.Children.Add(description);
            if (!feature.IsSpatial)
            {
                var arrange = _chrome.ActionButton(UiText.Get("Arrange on screen"));
                arrange.Height = 44d;
                arrange.HorizontalAlignment = HorizontalAlignment.Left;
                arrange.Margin = new Thickness(0d, 12d, 0d, 0d);
                arrange.Click += (sender, args) => OpenLayout(feature.Key);
                copy.Children.Add(arrange);
            }
            Grid.SetColumn(copy, 1);
            grid.Children.Add(copy);

            if (showFeatureSwitch)
            {
                var enabled = _actions.IsFeatureEnabled(feature.Key);
                var switchName = UiText.Get("Show in game") + ": " + feature.Title;
                var switchHost = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(12d, 8d, 0d, 0d)
                };
                var state = _palette.Text(UiText.Get(enabled ? "On" : "Off"), 12d, FontWeights.SemiBold,
                    _palette.MutedBrush);
                state.VerticalAlignment = VerticalAlignment.Center;
                state.Margin = new Thickness(0d, 0d, 8d, 0d);
                switchHost.Children.Add(state);
                switchHost.Children.Add(BuildSwitch(
                    switchName,
                    enabled,
                    next =>
                    {
                        _actions.SetWidgetEnabled(feature.Key, next);
                        Refresh();
                        RestoreFocusAfterRender(switchName);
                    }));
                Grid.SetColumn(switchHost, 2);
                grid.Children.Add(switchHost);
            }

            var header = new Border
            {
                Background = _palette.SurfaceBrush,
                BorderBrush = _palette.SeparatorBrush,
                BorderThickness = new Thickness(0d, 0d, 0d, 1d),
                Child = grid
            };
            AutomationProperties.SetName(header, feature.Title);
            return header;
        }

        private FrameworkElement BuildResourceWorkerSettings()
        {
            var settings = new StackPanel { Margin = new Thickness(0d, 20d, 0d, 0d) };
            settings.Children.Add(BuildResourceWorkerSetting(
                HudWidgetRegistry.MineralWorkers,
                UiText.Get("Minerals"),
                UiText.Get("Mineral worker counts by base."),
                FeatureSettingsPreviewKind.Mineral,
                true));
            settings.Children.Add(BuildResourceWorkerSetting(
                HudWidgetRegistry.GasWorkers,
                UiText.Get("Gas"),
                UiText.Get("Gas worker counts by refinery."),
                FeatureSettingsPreviewKind.Gas,
                false));
            return settings;
        }

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

        private FrameworkElement BuildTransportCargoSettings()
        {
            var title = UiText.Get("Passenger icons and counts");
            var row = new Grid { Margin = new Thickness(16d) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(52d) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var icon = _previewFactory.BuildFeatureIcon(
                FeatureSettingsPreviewKind.Transport,
                40d);
            icon.VerticalAlignment = VerticalAlignment.Center;
            row.Children.Add(icon);

            var copy = new StackPanel
            {
                Margin = new Thickness(12d, 0d, 16d, 0d),
                VerticalAlignment = VerticalAlignment.Center
            };
            copy.Children.Add(_palette.Text(title, 14d, FontWeights.Bold, _palette.TextBrush));
            var description = _palette.Text(
                UiText.Get("Show passenger unit icons and counts inside loaded transports."),
                12d,
                FontWeights.Normal,
                _palette.MutedBrush);
            description.Margin = new Thickness(0d, 3d, 0d, 0d);
            description.TextWrapping = TextWrapping.Wrap;
            copy.Children.Add(description);
            Grid.SetColumn(copy, 1);
            row.Children.Add(copy);

            var enabled = _actions.Layout.ShowTransportCargo;
            var switchName = UiText.Get("Show in game") + ": " + title;
            var switchHost = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            var state = _palette.Text(UiText.Get(enabled ? "On" : "Off"), 12d, FontWeights.SemiBold,
                _palette.MutedBrush);
            state.VerticalAlignment = VerticalAlignment.Center;
            state.Margin = new Thickness(0d, 0d, 8d, 0d);
            switchHost.Children.Add(state);
            switchHost.Children.Add(BuildSwitch(
                switchName,
                enabled,
                next =>
                {
                    _actions.ApplyEdit(SettingsEdit.SetTransportCargoVisible(next));
                    _actions.RefreshPresenterViews();
                    _actions.RefreshSpatialPresentation();
                    _actions.RefreshVisibility();
                    Refresh();
                    RestoreFocusAfterRender(switchName);
                }));
            Grid.SetColumn(switchHost, 2);
            row.Children.Add(switchHost);

            return new Border
            {
                Margin = new Thickness(0d, 20d, 0d, 0d),
                Background = _palette.SurfaceBrush,
                BorderBrush = _palette.SeparatorBrush,
                BorderThickness = new Thickness(0d, 1d, 0d, 1d),
                Child = row
            };
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

            var current = _actions.Layout.GetAbilityDisplayMode(definition.UnitId);
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

        private FrameworkElement BuildResourceWorkerSetting(
            string featureKey,
            string title,
            string description,
            FeatureSettingsPreviewKind previewKind,
            bool first)
        {
            var row = new Grid { Margin = new Thickness(16d) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(52d) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var icon = _previewFactory.BuildFeatureIcon(previewKind, 40d);
            icon.VerticalAlignment = VerticalAlignment.Center;
            row.Children.Add(icon);

            var copy = new StackPanel
            {
                Margin = new Thickness(12d, 0d, 16d, 0d),
                VerticalAlignment = VerticalAlignment.Center
            };
            copy.Children.Add(_palette.Text(title, 14d, FontWeights.Bold, _palette.TextBrush));
            var descriptionText = _palette.Text(description, 12d, FontWeights.Normal, _palette.MutedBrush);
            descriptionText.Margin = new Thickness(0d, 3d, 0d, 0d);
            descriptionText.TextWrapping = TextWrapping.Wrap;
            copy.Children.Add(descriptionText);
            Grid.SetColumn(copy, 1);
            row.Children.Add(copy);

            var enabled = _actions.IsFeatureEnabled(featureKey);
            var switchName = UiText.Get("Show in game") + ": " + title;
            var switchHost = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            var state = _palette.Text(UiText.Get(enabled ? "On" : "Off"), 12d, FontWeights.SemiBold,
                _palette.MutedBrush);
            state.VerticalAlignment = VerticalAlignment.Center;
            state.Margin = new Thickness(0d, 0d, 8d, 0d);
            switchHost.Children.Add(state);
            switchHost.Children.Add(BuildSwitch(
                switchName,
                enabled,
                next =>
                {
                    _actions.SetWidgetEnabled(featureKey, next);
                    Refresh();
                    RestoreFocusAfterRender(switchName);
                }));
            Grid.SetColumn(switchHost, 2);
            row.Children.Add(switchHost);

            var setting = new Border
            {
                Background = _palette.SurfaceBrush,
                BorderBrush = _palette.SeparatorBrush,
                BorderThickness = first ? new Thickness(0d, 1d, 0d, 1d) : new Thickness(0d, 0d, 0d, 1d),
                Child = row
            };
            AutomationProperties.SetName(setting, title);
            return setting;
        }

    }
}
