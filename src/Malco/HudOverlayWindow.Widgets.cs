using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Malco.Configuration.Models;
using Malco.Data;
using Malco.Localization;
using Malco.Models;
using Malco.Presentation.Hud.Tiles;
using Malco.Presentation.Hud.Upgrades;
using Malco.Settings.Contracts;
using Malco.Settings.Views;

namespace Malco
{
    internal sealed partial class HudOverlayWindow
    {
        private void BuildWidgets()
        {
            BuildSettingsButton();

            AddWidget(HudWidgetRegistry.WorkersWidget, _workersPresenter.WorkerView.Body);

            AddWidget(HudWidgetRegistry.UnitsWidget, _unitsPresenter.View.Body);

            AddWidget(HudWidgetRegistry.BuildingsWidget, _buildingsPresenter.View.Body);

            AddWidget(HudWidgetRegistry.UpgradesWidget, _upgradesPresenter.CompletedView.Body);
            AddWidget(HudWidgetRegistry.UpgradeCompletionWarningsWidget, _upgradesPresenter.WarningView);
            AddWidget(HudWidgetRegistry.AvailableUpgradesWidget, _upgradesPresenter.AvailableView.Panel);
        }

        private void BuildSettingsButton()
        {
            _settingsButton.Content = UiText.Get("Settings");
            _settingsButton.Width = 104d;
            _settingsButton.Height = 34d;
            _settingsButton.Padding = new Thickness(10d, 0d, 10d, 0d);
            _settingsButton.Foreground = SettingsTextBrush;
            _settingsButton.FontSize = 12d;
            _settingsButton.FontWeight = FontWeights.SemiBold;
            _settingsButton.Background = SettingsRaisedSurfaceBrush;
            _settingsButton.BorderBrush = SettingsAccentBrush;
            _settingsButton.BorderThickness = new Thickness(1d);
            _settingsButton.Style = _settingsChrome.ButtonStyle();
            _settingsButton.Visibility = Visibility.Collapsed;
            _settingsButton.Click += (sender, args) => HandleSettingsIntent(
                new SettingsIntent(SettingsIntentKind.OpenFeatures));
            Panel.SetZIndex(_settingsButton, 80);
            _hudCanvas.Children.Add(_settingsButton);
            Canvas.SetLeft(_settingsButton, 0d);
            Canvas.SetTop(_settingsButton, 0d);
        }

        private void AddWidget(
            HudWidgetDefinition definition,
            UIElement body)
        {
            var layout = _settingsController.Layout.GetOrCreate(
                definition.Key,
                definition.X,
                definition.Y,
                definition.Width,
                definition.Height,
                definition.EnabledByDefault);
            var widget = new HudWidgetView(
                definition.Key,
                UiText.Get(definition.Title),
                layout,
                body,
                BuildLayoutSample(definition.Key),
                SettingsTextBrush);
            widget.Selected += _layoutEditorView.OnCanvasWidgetSelected;
            widget.LayoutChanged += OnWidgetLayoutChanged;
            _widgets[definition.Key] = widget;
            _hudVisualTree.Attach(widget.Handle);
            SetWidgetGameplayContent(definition.Key, false);
            widget.ApplyBounds(_hudCanvas, GetHudUiScale(_hudCanvas.ActualWidth, _hudCanvas.ActualHeight));
        }

        private UIElement BuildLayoutSample(string key)
        {
            var catalog = TechTreeCatalog.GetRaceCatalog(Race.Terran);
            if (string.Equals(key, HudWidgetRegistry.Workers, StringComparison.OrdinalIgnoreCase))
            {
                var workers = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                var worker = TechTreeItem.Unit(LayoutSampleWorkerUnitId(catalog.Race));
                workers.Children.Add(BuildLayoutSampleStat(worker, UiText.Get("2 idle"), true));
                workers.Children.Add(BuildLayoutSampleStat(worker, UiText.Get("24 total")));
                return workers;
            }

            if (string.Equals(key, HudWidgetRegistry.UpgradeCompletionWarnings, StringComparison.OrdinalIgnoreCase))
            {
                var warnings = new StackPanel { Margin = new Thickness(8d, 8d, 8d, 4d) };
                foreach (var item in LayoutSampleCompletionWarningItems())
                {
                    warnings.Children.Add(BuildLayoutSampleWarning(
                        item,
                        UiText.GameName(item.Name),
                        UpgradePresentationIdentity.FormatRemainingSeconds(
                            MalcoPreferenceValues.DefaultCompletionCountdownSeconds)));
                }
                return warnings;
            }

            var tiles = new WrapPanel
            {
                Margin = new Thickness(8d, 2d, 8d, 2d),
                VerticalAlignment = VerticalAlignment.Top
            };
            if (string.Equals(key, HudWidgetRegistry.Units, StringComparison.OrdinalIgnoreCase))
            {
                foreach (var item in TechTreeCatalog.GetDisplayUnits(catalog.Race))
                    tiles.Children.Add(BuildLayoutSampleTile(item, "1", false, _settingsController.Layout.GetIconSize(key)));
            }
            else if (string.Equals(key, HudWidgetRegistry.Buildings, StringComparison.OrdinalIgnoreCase))
            {
                foreach (var item in catalog.Branches.Select(branch => branch.Building))
                    tiles.Children.Add(BuildLayoutSampleTile(item, "1", false, _settingsController.Layout.GetIconSize(key)));
            }
            else if (string.Equals(key, HudWidgetRegistry.AvailableUpgrades, StringComparison.OrdinalIgnoreCase))
            {
                tiles.Margin = new Thickness(0d);
                foreach (var item in LayoutSampleResearchItems(catalog))
                    tiles.Children.Add(BuildLayoutSampleTile(item, string.Empty, false, _settingsController.Layout.GetIconSize(key), true));
            }
            else if (string.Equals(key, HudWidgetRegistry.Upgrades, StringComparison.OrdinalIgnoreCase))
            {
                foreach (var item in LayoutSampleResearchItems(catalog))
                    tiles.Children.Add(BuildLayoutSampleTile(
                        item,
                        LayoutSampleUpgradeBadge(item),
                        false,
                        _settingsController.Layout.GetIconSize(key)));
            }

            return tiles;
        }

        private static IEnumerable<TechTreeItem> LayoutSampleResearchItems(TechTreeRaceCatalog catalog)
        {
            return catalog.Branches
                .SelectMany(branch => branch.Items)
                .Where(item =>
                    item.Kind == TechTreeItemKind.Upgrade ||
                    item.Kind == TechTreeItemKind.Tech)
                .GroupBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First());
        }

        private static IEnumerable<TechTreeItem> LayoutSampleCompletionWarningItems()
        {
            yield return TechTreeItem.Tech("Stim_Packs");
            yield return TechTreeItem.Upgrade("Terran_Infantry_Weapons");
        }

        private static string LayoutSampleUpgradeBadge(TechTreeItem item)
        {
            if (item.Kind != TechTreeItemKind.Upgrade)
            {
                return string.Empty;
            }

            var maxLevel = Math.Max(1, BwapiBroodWarTables.GetUpgradeMaxLevel(item.UpgradeIndex));
            return maxLevel > 1
                ? "+" + maxLevel.ToString(CultureInfo.InvariantCulture)
                : string.Empty;
        }

        private static int LayoutSampleWorkerUnitId(Race race)
        {
            switch (race)
            {
                case Race.Zerg: return 41;
                case Race.Protoss: return 64;
                default: return 7;
            }
        }

        private void RefreshLayoutSamples()
        {
            foreach (var definition in HudWidgetRegistry.EditorFeatures())
            {
                HudWidgetView widget;
                if (_widgets.TryGetValue(definition.Key, out widget) && widget != null)
                {
                    widget.SetSampleBody(BuildLayoutSample(definition.Key));
                }
            }
        }

        private FrameworkElement BuildLayoutSampleStat(TechTreeItem item, string value, bool showAlert = false)
        {
            var stat = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(5d, 0d, 5d, 0d),
                VerticalAlignment = VerticalAlignment.Center
            };
            var iconHost = new Grid { Width = 16d, Height = 16d };
            iconHost.Children.Add(BuildLayoutSampleIcon(item, 16d));
            if (showAlert)
            {
                var alert = Text("!", 12d, FontWeights.Black, CoralBrush);
                alert.HorizontalAlignment = HorizontalAlignment.Center;
                alert.VerticalAlignment = VerticalAlignment.Center;
                alert.Margin = new Thickness(0d, -2d, 0d, 0d);
                iconHost.Children.Add(alert);
            }
            stat.Children.Add(iconHost);
            var text = Text(value, 11d, FontWeights.Bold, TextBrush);
            text.Margin = new Thickness(4d, 0d, 0d, 0d);
            text.VerticalAlignment = VerticalAlignment.Center;
            stat.Children.Add(text);
            return stat;
        }

        private FrameworkElement BuildLayoutSampleTile(
            TechTreeItem item,
            string badge,
            bool dimmed,
            string iconSize,
            bool availableUpgrade = false)
        {
            var tileWidth = MalcoPreferenceValues.IconTileWidth(iconSize);
            var tileGap = MalcoPreferenceValues.IconTileGap(iconSize);
            var isUnitOrBuilding = item.Kind == TechTreeItemKind.Building || item.Kind == TechTreeItemKind.Unit;
            ImageSource image = isUnitOrBuilding
                ? _hudTileFactory.GetGrayscaleUnitIcon(item.ToUnitCount())
                : _icons.GetUpgradeIcon(item.ToUpgradeState());
            if (!availableUpgrade)
            {
                var previewName = UiText.Get(dimmed ? "Blocked preview" : "Preview") + ": " + UiText.GameName(item.Name);
                var tile = (FrameworkElement)_hudTileFactory.BuildImageTile(
                    image,
                    item.Name.Substring(0, 1).ToUpperInvariant(),
                    badge,
                    previewName,
                    TileMetrics.FromWidth(tileWidth, tileGap),
                    !isUnitOrBuilding,
                    isUnitOrBuilding ? HudTileBadgeStyle.Count : HudTileBadgeStyle.UpgradeLevel);
                tile.Opacity = dimmed ? 0.38d : 1d;
                AutomationProperties.SetName(tile, previewName + (string.IsNullOrEmpty(badge) ? string.Empty : " " + badge));
                return tile;
            }

            var metrics = TileMetrics.FromWidth(tileWidth, tileGap);
            var grid = new Grid
            {
                Width = metrics.Width,
                Height = metrics.FrameHeight,
                Margin = new Thickness(0d, 0d, metrics.Gap, 0d),
                Opacity = dimmed ? 0.35d : 1d,
                ToolTip = UiText.Get(dimmed ? "Blocked preview" : "Preview") + ": " + UiText.GameName(item.Name)
            };
            var frame = new Border
            {
                Width = metrics.FrameWidth,
                Height = metrics.FrameHeight,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                CornerRadius = new CornerRadius(6d),
                Background = ChipBackgroundBrush,
                BorderBrush = ChipBorderBrush,
                BorderThickness = new Thickness(1d)
            };
            var iconSizeWithinFrame = Math.Max(
                1d,
                Math.Min(metrics.FrameWidth, metrics.FrameHeight) - metrics.IconMargin * 2d);
            var icon = BuildLayoutSampleIcon(item, iconSizeWithinFrame);
            icon.Margin = new Thickness(metrics.IconMargin);
            frame.Child = icon;
            grid.Children.Add(frame);
            AutomationProperties.SetName(
                grid,
                UiText.Get(dimmed ? "Blocked preview" : "Preview") + ": " + UiText.GameName(item.Name) +
                (string.IsNullOrEmpty(badge) ? string.Empty : " " + badge));
            return grid;
        }

        private FrameworkElement BuildLayoutSampleWarning(TechTreeItem item, string name, string remaining)
        {
            var row = new DockPanel
            {
                Height = 42d,
                Margin = new Thickness(0d, 0d, 0d, 5d)
            };
            var icon = BuildLayoutSampleIcon(item, UpgradeTileFactory.CompletionIconSize);
            DockPanel.SetDock(icon, Dock.Left);
            row.Children.Add(icon);
            var time = Text(remaining, 13d, FontWeights.Bold, AmberBrush);
            time.Margin = new Thickness(8d, 0d, 2d, 0d);
            time.VerticalAlignment = VerticalAlignment.Center;
            DockPanel.SetDock(time, Dock.Right);
            row.Children.Add(time);
            var label = Text(name, 11d, FontWeights.SemiBold, TextBrush);
            label.Margin = new Thickness(8d, 0d, 4d, 0d);
            label.VerticalAlignment = VerticalAlignment.Center;
            row.Children.Add(label);
            return row;
        }

        private FrameworkElement BuildLayoutSampleIcon(TechTreeItem item, double size)
        {
            var isUnitOrBuilding = item.Kind == TechTreeItemKind.Building || item.Kind == TechTreeItemKind.Unit;
            ImageSource image = isUnitOrBuilding
                ? _hudTileFactory.GetGrayscaleUnitIcon(item.ToUnitCount())
                : _icons.GetUpgradeIcon(item.ToUpgradeState());
            if (image != null)
            {
                return new Image
                {
                    Width = size,
                    Height = size,
                    Source = isUnitOrBuilding ? image : GrayscaleIcon(image),
                    Stretch = Stretch.Uniform,
                    Opacity = 0.8d
                };
            }

            var fallback = Text(item.Name.Substring(0, 1).ToUpperInvariant(), 12d, FontWeights.Bold, TextBrush);
            fallback.Width = size;
            fallback.Height = size;
            fallback.TextAlignment = TextAlignment.Center;
            return fallback;
        }
    }
}
