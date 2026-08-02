using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Malco.Data;
using Malco.Localization;
using Malco.Models;

namespace Malco.Settings.Views
{
    internal sealed class FeatureSettingsPreviewFactory
    {
        private readonly SettingsViewPalette _palette;
        private IconLocator _icons;

        public FeatureSettingsPreviewFactory(SettingsViewPalette palette)
        {
            _palette = palette ?? throw new ArgumentNullException(
                nameof(palette));
        }

        public void SetIconLocator(IconLocator icons)
        {
            _icons = icons;
        }

        public FrameworkElement BuildFeaturePreview(
            FeatureSettingsPreviewKind kind)
        {
            return new Border
            {
                Width = 88d,
                Height = 88d,
                Background = _palette.PanelBrush,
                BorderBrush = _palette.BorderBrush,
                BorderThickness = new Thickness(1d),
                CornerRadius = new CornerRadius(3d),
                Child = BuildFeatureIcon(kind, 48d)
            };
        }

        public FrameworkElement BuildFeatureIcon(
            FeatureSettingsPreviewKind kind,
            double size,
            bool muted = false)
        {
            var item = PreviewItem(kind);
            if (item != null)
            {
                return BuildCatalogIcon(item, size, muted);
            }
            if (kind == FeatureSettingsPreviewKind.Command ||
                kind == FeatureSettingsPreviewKind.Rally)
            {
                var lineBrush =
                    muted ? _palette.MutedBrush : _palette.AccentBrush;
                var linePreview = new Canvas
                {
                    Width = size,
                    Height = 12d,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                linePreview.Children.Add(new System.Windows.Shapes.Line
                {
                    X1 = 2d,
                    Y1 = 6d,
                    X2 = Math.Max(3d, size - 5d),
                    Y2 = 6d,
                    Stroke = lineBrush,
                    StrokeThickness = 1d,
                    StrokeDashArray =
                        kind == FeatureSettingsPreviewKind.Rally
                            ? new DoubleCollection { 7d, 3d }
                            : new DoubleCollection { 4d, 3d },
                    Opacity = muted ? 0.3d : 0.44d
                });
                var endpoint = new System.Windows.Shapes.Ellipse
                {
                    Width = 4d,
                    Height = 4d,
                    Stroke = lineBrush,
                    StrokeThickness = 1d,
                    Opacity = muted ? 0.3d : 0.46d
                };
                Canvas.SetLeft(endpoint, Math.Max(0d, size - 5d));
                Canvas.SetTop(endpoint, 4d);
                linePreview.Children.Add(endpoint);
                return linePreview;
            }
            if (kind == FeatureSettingsPreviewKind.Resources)
            {
                var resources = new StackPanel
                {
                    Width = size,
                    Height = size,
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                var badgeSize = Math.Max(12d, (size - 4d) / 2d);
                resources.Children.Add(
                    BuildResourceBadge("M", badgeSize, muted));
                var gas = BuildResourceBadge("G", badgeSize, muted);
                gas.Margin = new Thickness(4d, 0d, 0d, 0d);
                resources.Children.Add(gas);
                return resources;
            }
            if (kind == FeatureSettingsPreviewKind.Mineral ||
                kind == FeatureSettingsPreviewKind.Gas)
            {
                return BuildResourceBadge(
                    kind == FeatureSettingsPreviewKind.Mineral ? "M" : "G",
                    size,
                    muted);
            }
            if (kind == FeatureSettingsPreviewKind.Ability)
            {
                return new System.Windows.Shapes.Ellipse
                {
                    Width = Math.Max(9d, size * .28d),
                    Height = Math.Max(9d, size * .28d),
                    Fill = muted
                        ? _palette.MutedBrush
                        : new SolidColorBrush(Color.FromArgb(153, 239, 42, 55)),
                    Stroke = Brushes.White,
                    StrokeThickness = 1d,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
            }
            var label = _palette.Text(
                kind == FeatureSettingsPreviewKind.General ? "Aa" : "10s",
                Math.Max(12d, size * .34d),
                FontWeights.Bold,
                _palette.TextBrush);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.VerticalAlignment = VerticalAlignment.Center;
            label.TextAlignment = TextAlignment.Center;
            return label;
        }

        public FrameworkElement BuildCatalogIcon(
            TechTreeItem item,
            double size,
            bool muted = false)
        {
            ImageSource source = null;
            if (_icons != null)
            {
                source =
                    item.Kind == TechTreeItemKind.Building ||
                    item.Kind == TechTreeItemKind.Unit
                        ? _icons.GetUnitIcon(item.ToUnitCount())
                        : _icons.GetUpgradeIcon(item.ToUpgradeState());
            }
            if (muted && source != null)
            {
                source = _palette.GrayscaleIcon(source);
            }
            var host = new Border
            {
                Width = size,
                Height = size,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Background = _palette.ChipBackgroundBrush,
                BorderBrush = _palette.ChipBorderBrush,
                BorderThickness = new Thickness(1d)
            };
            host.Child = source != null
                ? (UIElement)new Image
                {
                    Source = source,
                    Stretch = Stretch.Uniform,
                    Opacity = muted ? 0.72d : 1d
                }
                : _palette.Text(
                    UiText.GameName(item.Name).Substring(0, 1),
                    12d,
                    FontWeights.Bold,
                    _palette.TextBrush);
            AutomationProperties.SetName(
                host,
                UiText.GameName(item.Name) + " " + UiText.Get("icon"));
            return host;
        }

        public ImageSource ResolveUpgradeIcon(UpgradeState state)
        {
            return _icons?.GetUpgradeIcon(state);
        }

        private Border BuildResourceBadge(
            string label,
            double size,
            bool muted)
        {
            var resourceLabel = _palette.Text(
                label,
                Math.Max(10d, size * .4d),
                FontWeights.Bold,
                muted ? _palette.MutedBrush : _palette.TextBrush);
            resourceLabel.HorizontalAlignment = HorizontalAlignment.Center;
            resourceLabel.VerticalAlignment = VerticalAlignment.Center;
            return new Border
            {
                Width = size,
                Height = size,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Background = _palette.ChipBackgroundBrush,
                BorderBrush =
                    muted ? _palette.MutedBrush : _palette.AccentBrush,
                BorderThickness = new Thickness(1d),
                Child = resourceLabel
            };
        }

        private static TechTreeItem PreviewItem(
            FeatureSettingsPreviewKind kind)
        {
            switch (kind)
            {
                case FeatureSettingsPreviewKind.Workers:
                    return TechTreeItem.Unit(7);
                case FeatureSettingsPreviewKind.Units:
                    return TechTreeItem.Unit(0);
                case FeatureSettingsPreviewKind.Buildings:
                    return TechTreeItem.Building(111);
                case FeatureSettingsPreviewKind.Transport:
                    return TechTreeItem.Unit(11);
                case FeatureSettingsPreviewKind.Completed:
                case FeatureSettingsPreviewKind.Available:
                case FeatureSettingsPreviewKind.Progress:
                    return TechTreeItem.Upgrade(
                        "Terran_Infantry_Weapons");
                default:
                    return null;
            }
        }
    }
}
