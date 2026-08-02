using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Malco.Localization;
using Malco.Models;
using Malco.Presentation.Hud.Tiles;

namespace Malco.Presentation.Hud.Upgrades
{
    internal sealed class UpgradeTileFactory
    {
        internal const double CompletionIconSize = 20d;

        private readonly HudTileFactory _tiles;
        private readonly Brush _textBrush;
        private readonly Brush _mutedBrush;
        private readonly Brush _amberBrush;
        private readonly Brush _chipBackgroundBrush;
        private readonly Brush _chipBorderBrush;

        public UpgradeTileFactory(HudTileFactory tiles, Brush textBrush, Brush mutedBrush, Brush amberBrush, Brush chipBackgroundBrush, Brush chipBorderBrush)
        {
            _tiles = tiles ?? throw new ArgumentNullException(nameof(tiles));
            _textBrush = textBrush ?? throw new ArgumentNullException(nameof(textBrush));
            _mutedBrush = mutedBrush ?? throw new ArgumentNullException(nameof(mutedBrush));
            _amberBrush = amberBrush ?? throw new ArgumentNullException(nameof(amberBrush));
            _chipBackgroundBrush = chipBackgroundBrush ?? throw new ArgumentNullException(nameof(chipBackgroundBrush));
            _chipBorderBrush = chipBorderBrush ?? throw new ArgumentNullException(nameof(chipBorderBrush));
        }

        public UIElement BuildCompleted(UpgradeState state, TileMetrics metrics)
        {
            var isTech = state.Name.IndexOf("Tech ", StringComparison.OrdinalIgnoreCase) == 0;
            var value = state.IsComplete && !isTech ? UpgradePresentationIdentity.CompletedValue(state) : string.Empty;
            var name = LocalizedName(state);
            return _tiles.BuildImageTile(
                _tiles.GetUpgradeIcon(state),
                UpgradePresentationIdentity.FallbackLabel(state),
                value,
                name,
                metrics,
                true,
                HudTileBadgeStyle.Count);
        }

        public UIElement BuildAvailable(UpgradeState state, TileMetrics metrics)
        {
            var name = LocalizedName(state);
            var status = UiText.Get(state.IsBlocked ? "Blocked" : "Available");
            var grid = new Grid
            {
                Width = metrics.Width,
                Height = metrics.FrameHeight,
                Margin = new Thickness(0d, 0d, metrics.Gap, 0d),
                ToolTip = status + ": " + name
            };
            HudTileFactory.SetHideWhenClipped(grid, true);
            AutomationProperties.SetName(grid, status + " " + name);
            var frame = new Border
            {
                Width = metrics.FrameWidth,
                Height = metrics.FrameHeight,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                CornerRadius = new CornerRadius(6d),
                Background = _chipBackgroundBrush,
                BorderBrush = _chipBorderBrush,
                BorderThickness = new Thickness(1d)
            };
            var icon = _tiles.GetUpgradeIcon(state);
            if (icon != null)
            {
                var content = new Grid();
                var image = new Image
                {
                    Source = state.IsBlocked ? _tiles.GetGrayscaleIcon(icon) : icon,
                    Stretch = Stretch.Uniform,
                    Margin = new Thickness(metrics.IconMargin),
                    Opacity = 0.8d
                };
                if (state.IsBlocked) image.Opacity = 0.35d;
                content.Children.Add(image);
                if (state.IsBlocked)
                {
                    content.Children.Add(new Border
                    {
                        CornerRadius = new CornerRadius(4d),
                        Background = new SolidColorBrush(Color.FromArgb(110, 95, 102, 112)),
                        Margin = new Thickness(metrics.IconMargin)
                    });
                }
                frame.Child = content;
            }
            else
            {
                var fallback = Text(
                    UpgradePresentationIdentity.FallbackLabel(state),
                    metrics.FallbackFontSize,
                    FontWeights.Bold,
                    _textBrush);
                fallback.HorizontalAlignment = HorizontalAlignment.Center;
                fallback.VerticalAlignment = VerticalAlignment.Center;
                if (state.IsBlocked)
                {
                    fallback.Opacity = 0.35d;
                    fallback.Foreground = _mutedBrush;
                }
                frame.Child = fallback;
            }
            grid.Children.Add(frame);
            return grid;
        }

        public UpgradeWarningVisual BuildWarning(string key, UpgradeState state, DateTime capturedAt)
        {
            var name = LocalizedName(state);
            var root = new Border
            {
                MinHeight = 32d,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0d, 0d, 0d, 3d),
                Padding = new Thickness(3d),
                CornerRadius = new CornerRadius(6d),
                Background = _chipBackgroundBrush,
                BorderBrush = _chipBorderBrush,
                BorderThickness = new Thickness(1d),
                ToolTip = name,
                RenderTransform = new TranslateTransform()
            };
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22d) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
            var icon = _tiles.GetUpgradeIcon(state);
            var iconHost = new Border
            {
                Width = CompletionIconSize,
                Height = CompletionIconSize,
                CornerRadius = new CornerRadius(5d),
                Background = _chipBackgroundBrush,
                BorderBrush = _chipBorderBrush,
                BorderThickness = new Thickness(1d)
            };
            iconHost.Child = icon != null
                ? (UIElement)new Image { Source = icon, Stretch = Stretch.Uniform, Margin = new Thickness(1.5d), Opacity = 0.8d }
                : Text(UpgradePresentationIdentity.FallbackLabel(state), 11d, FontWeights.Bold, _textBrush);
            grid.Children.Add(iconHost);
            var copy = new StackPanel { Margin = new Thickness(5d, 0d, 0d, 0d) };
            Grid.SetColumn(copy, 1);
            var countdown = Text(string.Empty, 14d, FontWeights.Black, _amberBrush);
            countdown.LineHeight = 15d;
            copy.Children.Add(countdown);
            var progress = new ProgressBar
            {
                Height = 3d,
                Minimum = 0d,
                Maximum = 100d,
                Margin = new Thickness(0d, 1d, 0d, 1d),
                Foreground = _amberBrush,
                Background = _chipBorderBrush,
                BorderThickness = new Thickness(0d),
                Visibility = Visibility.Collapsed,
                IsHitTestVisible = false
            };
            copy.Children.Add(progress);
            copy.Children.Add(Text(name, 9d, FontWeights.SemiBold, _textBrush));
            grid.Children.Add(copy);
            root.Child = grid;
            return new UpgradeWarningVisual(key, state, capturedAt, root, countdown, progress);
        }

        internal static string LocalizedName(UpgradeState state)
        {
            var compact = UpgradePresentationIdentity.CompactName(state != null ? state.Name : null);
            var levelMarker = compact.LastIndexOf(" +", StringComparison.Ordinal);
            int level;
            if (levelMarker > 0 && int.TryParse(compact.Substring(levelMarker + 2), out level))
            {
                return UiText.GameName(compact.Substring(0, levelMarker)) + compact.Substring(levelMarker);
            }
            return UiText.GameName(compact);
        }

        private static TextBlock Text(string text, double size, FontWeight weight, Brush brush) => new TextBlock
        {
            Text = text,
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = size,
            FontWeight = weight,
            Foreground = brush,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 4d, ShadowDepth = 1d, Opacity = .85d }
        };
    }
}
