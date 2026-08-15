using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Malco
{
    internal static class CargoIconRenderer
    {
        public static BitmapSource CreateOutlinedBitmap(BitmapSource bitmap)
        {
            var source = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0d);
            var stride = source.PixelWidth * 4;
            var pixels = new byte[stride * source.PixelHeight];
            source.CopyPixels(pixels, stride, 0);
            var outlined = (byte[])pixels.Clone();
            const int radius = 4;
            const int outerRadius = radius + 1;
            const int outerOutlineOpacity = 230;

            for (var y = 0; y < source.PixelHeight; y++)
            {
                for (var x = 0; x < source.PixelWidth; x++)
                {
                    var index = (y * stride) + (x * 4);
                    var shadowAlpha = 0;
                    for (var offsetY = -radius; offsetY <= radius; offsetY++)
                    {
                        var sampleY = y + offsetY;
                        if (sampleY < 0 || sampleY >= source.PixelHeight) continue;
                        for (var offsetX = -radius; offsetX <= radius; offsetX++)
                        {
                            var distanceSquared = (offsetX * offsetX) + (offsetY * offsetY);
                            if (distanceSquared == 0 || distanceSquared > radius * radius) continue;
                            var sampleX = x + offsetX;
                            if (sampleX < 0 || sampleX >= source.PixelWidth) continue;

                            var weight = CargoOutlineWeight(distanceSquared);
                            var sampleAlpha = pixels[(sampleY * stride) + (sampleX * 4) + 3];
                            shadowAlpha = Math.Max(shadowAlpha, sampleAlpha * weight / 255);
                        }
                    }

                    var sourceAlpha = pixels[index + 3];
                    var visibleShadow = shadowAlpha * (255 - sourceAlpha) / 255;
                    if (visibleShadow == 0) continue;

                    var combinedAlpha = Math.Min(255, sourceAlpha + visibleShadow);
                    outlined[index] = (byte)((pixels[index] * sourceAlpha + 255 * visibleShadow) / combinedAlpha);
                    outlined[index + 1] = (byte)((pixels[index + 1] * sourceAlpha + 255 * visibleShadow) / combinedAlpha);
                    outlined[index + 2] = (byte)((pixels[index + 2] * sourceAlpha + 255 * visibleShadow) / combinedAlpha);
                    outlined[index + 3] = (byte)combinedAlpha;
                }
            }

            var outerOutlined = (byte[])outlined.Clone();
            for (var y = 0; y < source.PixelHeight; y++)
            {
                for (var x = 0; x < source.PixelWidth; x++)
                {
                    var index = (y * stride) + (x * 4);
                    if (pixels[index + 3] != 0) continue;

                    var outerAlpha = 0;
                    for (var offsetY = -outerRadius; offsetY <= outerRadius; offsetY++)
                    {
                        var sampleY = y + offsetY;
                        if (sampleY < 0 || sampleY >= source.PixelHeight) continue;
                        for (var offsetX = -outerRadius; offsetX <= outerRadius; offsetX++)
                        {
                            var distanceSquared = (offsetX * offsetX) + (offsetY * offsetY);
                            if (distanceSquared <= radius * radius || distanceSquared > outerRadius * outerRadius) continue;
                            var sampleX = x + offsetX;
                            if (sampleX < 0 || sampleX >= source.PixelWidth) continue;

                            outerAlpha = Math.Max(
                                outerAlpha,
                                pixels[(sampleY * stride) + (sampleX * 4) + 3]);
                        }
                    }

                    if (outerAlpha == 0) continue;
                    outerOutlined[index] = 0;
                    outerOutlined[index + 1] = 0;
                    outerOutlined[index + 2] = 0;
                    outerOutlined[index + 3] = (byte)(outerAlpha * outerOutlineOpacity / 255);
                }
            }

            var result = BitmapSource.Create(
                source.PixelWidth,
                source.PixelHeight,
                source.DpiX,
                source.DpiY,
                PixelFormats.Bgra32,
                null,
                outerOutlined,
                stride);
            result.Freeze();
            return result;
        }

        private static int CargoOutlineWeight(int distanceSquared)
        {
            if (distanceSquared <= 2) return 220;
            if (distanceSquared <= 5) return 180;
            if (distanceSquared <= 10) return 130;
            return 75;
        }
    }
}
