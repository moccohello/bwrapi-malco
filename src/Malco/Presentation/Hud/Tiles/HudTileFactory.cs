using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Malco.Models;

namespace Malco.Presentation.Hud.Tiles
{
    internal enum HudTileBadgeStyle
    {
        Count,
        UpgradeLevel
    }

    internal sealed class HudTileFactory
    {
        public static readonly DependencyProperty HideWhenClippedProperty =
            DependencyProperty.RegisterAttached(
                "HideWhenClipped",
                typeof(bool),
                typeof(HudTileFactory),
                new FrameworkPropertyMetadata(false));

        private readonly IconLocator _icons;
        private readonly Brush _textBrush;
        private readonly Brush _amberBrush;
        private readonly Brush _chipBackgroundBrush;
        private readonly Brush _chipBorderBrush;
        private readonly Func<ImageSource, ImageSource> _grayscaleIcon;

        public HudTileFactory(
            IconLocator icons,
            Brush textBrush,
            Brush amberBrush,
            Brush chipBackgroundBrush,
            Brush chipBorderBrush,
            Func<ImageSource, ImageSource> grayscaleIcon)
        {
            _icons = icons ?? throw new ArgumentNullException(nameof(icons));
            _textBrush = textBrush ?? throw new ArgumentNullException(nameof(textBrush));
            _amberBrush = amberBrush ?? throw new ArgumentNullException(nameof(amberBrush));
            _chipBackgroundBrush = chipBackgroundBrush ?? throw new ArgumentNullException(nameof(chipBackgroundBrush));
            _chipBorderBrush = chipBorderBrush ?? throw new ArgumentNullException(nameof(chipBorderBrush));
            _grayscaleIcon = grayscaleIcon ?? throw new ArgumentNullException(nameof(grayscaleIcon));
        }

        public ImageSource GetGrayscaleUnitIcon(UnitCount unit) =>
            _icons.GetGrayscaleUnitIcon(unit) ?? _grayscaleIcon(_icons.GetUnitIcon(unit));

        public ImageSource GetUpgradeIcon(UpgradeState state) => _icons.GetUpgradeIcon(state);

        public ImageSource GetGrayscaleIcon(ImageSource image) => _grayscaleIcon(image);

        public static bool GetHideWhenClipped(DependencyObject value) =>
            value != null && (bool)value.GetValue(HideWhenClippedProperty);

        public static void SetHideWhenClipped(DependencyObject value, bool enabled)
        {
            if (value != null) value.SetValue(HideWhenClippedProperty, enabled);
        }

        public UIElement BuildImageTile(
            ImageSource image,
            string fallback,
            string value,
            string tooltip,
            TileMetrics metrics,
            bool grayscaleIcon,
            HudTileBadgeStyle badgeStyle)
        {
            var tile = new Grid
            {
                Width = metrics.Width,
                Height = metrics.Height,
                Margin = new Thickness(0d, 0d, metrics.Gap, 0d),
                ToolTip = tooltip
            };
            SetHideWhenClipped(tile, true);
            var frame = new Border
            {
                Width = metrics.FrameWidth,
                Height = metrics.FrameHeight,
                CornerRadius = new CornerRadius(6d),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                BorderBrush = _chipBorderBrush,
                BorderThickness = new Thickness(1d),
                Background = _chipBackgroundBrush
            };
            if (image != null)
            {
                frame.Child = new Image
                {
                    Source = grayscaleIcon ? _grayscaleIcon(image) : image,
                    Stretch = Stretch.Uniform,
                    Margin = new Thickness(metrics.IconMargin),
                    Opacity = 0.8d
                };
            }
            else
            {
                frame.Child = new TextBlock
                {
                    Text = fallback,
                    FontSize = metrics.FallbackFontSize,
                    FontWeight = FontWeights.Bold,
                    Foreground = _textBrush,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
            }

            tile.Children.Add(frame);
            if (!string.IsNullOrEmpty(value))
            {
                tile.Children.Add(new Border
                {
                    Width = metrics.BadgeWidth,
                    Height = metrics.BadgeHeight,
                    Margin = new Thickness(metrics.BadgeLeft, metrics.BadgeTop, 0d, 0d),
                    CornerRadius = new CornerRadius(6d),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    Background = _chipBackgroundBrush,
                    BorderBrush = _chipBorderBrush,
                    BorderThickness = new Thickness(1d),
                    Child = new TextBlock
                    {
                        Text = value,
                        Foreground = badgeStyle == HudTileBadgeStyle.Count ? _textBrush : _amberBrush,
                        FontSize = badgeStyle == HudTileBadgeStyle.Count ? metrics.BadgeFontSize : metrics.UpgradeBadgeFontSize,
                        FontWeight = FontWeights.Bold,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        TextTrimming = TextTrimming.CharacterEllipsis
                    }
                });
            }

            return tile;
        }
    }
}
