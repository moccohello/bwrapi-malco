using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using Malco.Configuration.Models;
using Malco.Data;
using Malco.Models;

namespace Malco.Presentation.Hud.Workers
{
    internal sealed class WorkersPresenter
    {
        private static readonly SolidColorBrush ClassicGreenBrush = FrozenBrush(Color.FromRgb(14, 227, 21));
        private static readonly DropShadowEffect ClassicGreenShadow = FrozenShadow(1.5d);
        private static readonly DropShadowEffect WhiteShadow = FrozenShadow(4d);
        private readonly IconLocator _icons;
        private readonly Brush _textBrush;
        private long _sessionGeneration = -1;
        private string _workerCountStyle = string.Empty;

        public WorkersPresenter(IconLocator icons, Brush textBrush)
        {
            _icons = icons ?? throw new ArgumentNullException(nameof(icons));
            _textBrush = textBrush ?? throw new ArgumentNullException(nameof(textBrush));

            WorkerView = BuildWorkerView();
            ApplyWorkerCountStyle(null);
        }

        public WorkerHudViewHandles WorkerView { get; }

        public void ResetSession(long generation)
        {
            _sessionGeneration = generation;
            WorkerView.IdleWorkersValue.Text = "-";
            WorkerView.TotalWorkersValue.Text = "-";
            WorkerView.IdleWorkerAlertMark.Visibility = Visibility.Collapsed;
            WorkerView.IdleWorkerIcon.Source = null;
            WorkerView.TotalWorkerIcon.Source = null;
        }

        public bool ApplyWorkers(
            FrozenSemanticSnapshot snapshot,
            long generation,
            HudDisplayPreferences preferences)
        {
            EnsureSession(generation);
            ApplyWorkerCountStyle(preferences);
            UpdateWorkerIconImages(snapshot);
            if (snapshot == null ||
                !snapshot.IsInMatch ||
                snapshot.LocalPlayerId < 0 ||
                snapshot.Race == Race.Unknown)
            {
                WorkerView.TotalWorkersValue.Text = "-";
                WorkerView.IdleWorkersValue.Text = "-";
                WorkerView.IdleWorkerAlertMark.Visibility = Visibility.Collapsed;
                return false;
            }

            var idle = Math.Max(0, snapshot.WorkersIdle);
            WorkerView.IdleWorkersValue.Text = idle.ToString(CultureInfo.InvariantCulture);

            WorkerView.IdleWorkerAlertMark.Visibility = idle > 0
                ? Visibility.Visible
                : Visibility.Collapsed;
            WorkerView.TotalWorkersValue.Text = Math.Max(0, snapshot.WorkersTotal)
                .ToString(CultureInfo.InvariantCulture);
            return true;
        }

        private void ApplyWorkerCountStyle(HudDisplayPreferences preferences)
        {
            var style = MalcoPreferenceValues.NormalizeWorkerCountStyle(preferences?.WorkerCountStyle);
            if (string.Equals(_workerCountStyle, style, StringComparison.Ordinal))
            {
                return;
            }

            var classicGreen = string.Equals(
                style,
                MalcoPreferenceValues.WorkerCountClassicGreen,
                StringComparison.Ordinal);
            ApplyWorkerCountTextStyle(WorkerView.IdleWorkersValue, classicGreen);
            ApplyWorkerCountTextStyle(WorkerView.TotalWorkersValue, classicGreen);
            _workerCountStyle = style;
        }

        private void ApplyWorkerCountTextStyle(TextBlock value, bool classicGreen)
        {
            value.FontSize = 19d;
            value.FontWeight = classicGreen ? FontWeights.SemiBold : FontWeights.Bold;
            value.Foreground = classicGreen ? ClassicGreenBrush : _textBrush;
            value.Effect = classicGreen ? ClassicGreenShadow : WhiteShadow;
        }

        private void EnsureSession(long generation)
        {
            if (_sessionGeneration != generation)
            {
                ResetSession(generation);
            }
        }

        private void UpdateWorkerIconImages(FrozenSemanticSnapshot snapshot)
        {
            var worker = GetWorkerUnit(snapshot);
            var grayscale = _icons.GetGrayscaleUnitIcon(worker) ?? GrayscaleIcon(_icons.GetUnitIcon(worker));
            WorkerView.IdleWorkerIcon.Source = grayscale;
            WorkerView.TotalWorkerIcon.Source = grayscale;
        }

        private WorkerHudViewHandles BuildWorkerView()
        {
            var body = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0d),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            var idleIcon = WorkerIcon();
            var totalIcon = WorkerIcon();
            var idleValue = Text("-", 19d, FontWeights.SemiBold, ClassicGreenBrush);
            var totalValue = Text("-", 19d, FontWeights.SemiBold, ClassicGreenBrush);
            var alertMark = new TextBlock
            {
                Text = "!",
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 14d,
                FontWeight = FontWeights.Black,
                Foreground = Brushes.Red,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0d, -2d, 0d, 0d)
            };
            alertMark.Visibility = Visibility.Collapsed;
            body.Children.Add(BuildWorkerHudItem(idleIcon, idleValue, alertMark));
            body.Children.Add(BuildWorkerHudItem(totalIcon, totalValue, null));
            return new WorkerHudViewHandles(body, idleIcon, totalIcon, idleValue, totalValue, alertMark);
        }

        private UIElement BuildWorkerHudItem(
            Image icon,
            TextBlock value,
            TextBlock alertMark)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0d, 0d, 8d, 0d),
                VerticalAlignment = VerticalAlignment.Center
            };
            panel.Children.Add(BuildWorkerGlyph(icon, alertMark, 16d));
            value.Margin = new Thickness(5d, 0d, 0d, 0d);
            value.VerticalAlignment = VerticalAlignment.Center;
            panel.Children.Add(value);
            return panel;
        }

        private UIElement BuildWorkerGlyph(
            Image icon,
            TextBlock alertMark,
            double size)
        {
            var grid = new Grid
            {
                Width = size,
                Height = size,
                ClipToBounds = false
            };
            grid.Children.Add(new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(96, 10, 12, 14)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(140, 255, 255, 255)),
                BorderThickness = new Thickness(1d)
            });
            grid.Children.Add(icon);
            if (alertMark != null)
            {
                grid.Children.Add(alertMark);
            }

            return grid;
        }

        private static Image WorkerIcon() => new Image
        {
            Stretch = Stretch.Uniform,
            Margin = new Thickness(1d),
            Opacity = 0.8d
        };

        private static TextBlock Text(string text, double size, FontWeight weight, Brush brush) => new TextBlock
        {
            Text = text,
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = size,
            FontWeight = weight,
            Foreground = brush,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Effect = ClassicGreenShadow
        };

        private static SolidColorBrush FrozenBrush(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        private static DropShadowEffect FrozenShadow(double blurRadius)
        {
            var effect = new DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = blurRadius,
                ShadowDepth = 1d,
                Opacity = .85d
            };
            effect.Freeze();
            return effect;
        }

        private static UnitCount GetWorkerUnit(FrozenSemanticSnapshot snapshot)
        {
            var unitId = 7;
            var name = "SCV";
            var iconKey = "terran/scv";
            if (snapshot != null && snapshot.Race == Race.Zerg)
            {
                unitId = 41;
                name = "Drone";
                iconKey = "zerg/drone";
            }
            else if (snapshot != null && snapshot.Race == Race.Protoss)
            {
                unitId = 64;
                name = "Probe";
                iconKey = "protoss/probe";
            }

            return new UnitCount { UnitId = unitId, Name = name, IconKey = iconKey, Count = 1 };
        }

        private static ImageSource GrayscaleIcon(ImageSource image)
        {
            var bitmap = image as BitmapSource;
            if (bitmap == null)
            {
                return image;
            }

            var source = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0d);
            var stride = source.PixelWidth * 4;
            var pixels = new byte[stride * source.PixelHeight];
            source.CopyPixels(pixels, stride, 0);
            for (var i = 0; i < pixels.Length; i += 4)
            {
                var blue = pixels[i];
                var green = pixels[i + 1];
                var red = pixels[i + 2];
                var gray = (byte)((red * 299 + green * 587 + blue * 114) / 1000);
                pixels[i] = gray;
                pixels[i + 1] = gray;
                pixels[i + 2] = gray;
            }

            var result = BitmapSource.Create(
                source.PixelWidth,
                source.PixelHeight,
                source.DpiX,
                source.DpiY,
                PixelFormats.Bgra32,
                null,
                pixels,
                stride);
            result.Freeze();
            return result;
        }
    }
}
