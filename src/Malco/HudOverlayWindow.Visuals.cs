using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

namespace Malco
{
    internal sealed partial class HudOverlayWindow
    {
        internal static TextBlock Text(string text, double size, FontWeight weight, Brush brush)
        {
            return new TextBlock
            {
                Text = text,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = size,
                FontWeight = weight,
                Foreground = brush,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 4d,
                    ShadowDepth = 1d,
                    Opacity = .85d
                }
            };
        }

        internal static TextBlock SettingsText(string text, double size, FontWeight weight, Brush brush)
        {
            return new TextBlock
            {
                Text = text,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = size,
                FontWeight = weight,
                Foreground = brush,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
        }

        private static SolidColorBrush FrozenBrush(string color)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
            brush.Freeze();
            return brush;
        }

        internal static ImageSource GrayscaleIcon(ImageSource image)
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
