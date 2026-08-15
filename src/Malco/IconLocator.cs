using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Malco.Models;

namespace Malco
{
    internal sealed class IconLocator
    {
        private static readonly Regex UpgradeLevelSuffix = new Regex(@"\s\+\d+$", RegexOptions.Compiled);
        private static readonly Dictionary<string, string> UpgradeAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "stim-packs", "stim-pack-tech" },
            { "tank-siege-mode", "siege-tech" }
        };

        private readonly string _iconRoot;
        private readonly string _grayscaleIconRoot;
        private readonly Dictionary<string, string> _upgradeFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ImageSource> _cache = new Dictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ImageSource> _cargoIconCache = new Dictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ImageSource> _normalizedUpgradeCache = new Dictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase);

        public IconLocator()
        {
            _iconRoot = FindIconRoot(AppDomain.CurrentDomain.BaseDirectory);
            _grayscaleIconRoot = string.IsNullOrEmpty(_iconRoot)
                ? string.Empty
                : Path.Combine(Path.GetDirectoryName(_iconRoot), "starcraft-icons-gray");
            IndexUpgradeFiles();
        }

        public ImageSource GetUnitIcon(UnitCount unit)
        {
            if (unit == null || string.IsNullOrEmpty(unit.IconKey) || string.IsNullOrEmpty(_iconRoot))
            {
                return null;
            }

            var relativePath = unit.IconKey.Replace('/', Path.DirectorySeparatorChar) + ".png";
            return Load(Path.Combine(_iconRoot, "units", relativePath)) ??
                   Load(Path.Combine(_iconRoot, "construction", relativePath));
        }

        public ImageSource GetGrayscaleUnitIcon(UnitCount unit)
        {
            if (unit == null ||
                string.IsNullOrEmpty(unit.IconKey) ||
                string.IsNullOrEmpty(_grayscaleIconRoot))
            {
                return null;
            }

            var relativePath = unit.IconKey.Replace('/', Path.DirectorySeparatorChar) + ".png";
            return Load(Path.Combine(_grayscaleIconRoot, "units", relativePath)) ??
                   Load(Path.Combine(_grayscaleIconRoot, "construction", relativePath));
        }

        public ImageSource GetCargoUnitIcon(UnitCount unit)
        {
            if (unit == null || string.IsNullOrEmpty(unit.IconKey) || string.IsNullOrEmpty(_iconRoot))
            {
                return null;
            }

            var relativePath = unit.IconKey.Replace('/', Path.DirectorySeparatorChar) + ".png";
            return LoadCargoIcon(Path.Combine(_iconRoot, "units", relativePath)) ??
                   LoadCargoIcon(Path.Combine(_iconRoot, "construction", relativePath));
        }

        public ImageSource GetUpgradeIcon(UpgradeState state)
        {
            if (state == null || string.IsNullOrEmpty(state.Name))
            {
                return null;
            }

            var unitIcon = GetUnitIconForStateKey(state.StateKey);
            if (unitIcon != null)
            {
                return unitIcon;
            }

            var name = state.Name;
            if (name.StartsWith("Upgrade ", StringComparison.OrdinalIgnoreCase))
            {
                name = name.Substring("Upgrade ".Length);
            }
            else if (name.StartsWith("Tech ", StringComparison.OrdinalIgnoreCase))
            {
                name = name.Substring("Tech ".Length);
            }

            name = UpgradeLevelSuffix.Replace(name, string.Empty);
            var key = Normalize(name);
            var withoutRace = TrimRacePrefix(key);
            string path;
            if (!_upgradeFiles.TryGetValue(key, out path) &&
                !_upgradeFiles.TryGetValue(withoutRace, out path))
            {
                string alias;
                if (!UpgradeAliases.TryGetValue(withoutRace, out alias) ||
                    !_upgradeFiles.TryGetValue(alias, out path))
                {
                    return null;
                }
            }

            return LoadNormalizedUpgrade(path);
        }

        private ImageSource LoadNormalizedUpgrade(string path)
        {
            ImageSource cached;
            if (_normalizedUpgradeCache.TryGetValue(path, out cached))
            {
                return cached;
            }

            var bitmap = Load(path) as BitmapSource;
            if (bitmap == null)
            {
                return null;
            }

            var source = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0d);
            var stride = source.PixelWidth * 4;
            var pixels = new byte[stride * source.PixelHeight];
            source.CopyPixels(pixels, stride, 0);
            var cornerIndexes = new[]
            {
                0,
                (source.PixelWidth - 1) * 4,
                (source.PixelHeight - 1) * stride,
                ((source.PixelHeight - 1) * stride) + ((source.PixelWidth - 1) * 4)
            };
            var opaqueCorners = cornerIndexes.All(index => pixels[index + 3] >= 250);
            var backgroundBlue = cornerIndexes.Sum(index => pixels[index]) / cornerIndexes.Length;
            var backgroundGreen = cornerIndexes.Sum(index => pixels[index + 1]) / cornerIndexes.Length;
            var backgroundRed = cornerIndexes.Sum(index => pixels[index + 2]) / cornerIndexes.Length;
            var backgroundIsDark = backgroundBlue + backgroundGreen + backgroundRed < 90;

            var minX = source.PixelWidth;
            var minY = source.PixelHeight;
            var maxX = -1;
            var maxY = -1;
            for (var y = 0; y < source.PixelHeight; y++)
            {
                for (var x = 0; x < source.PixelWidth; x++)
                {
                    var index = (y * stride) + (x * 4);
                    if (opaqueCorners && backgroundIsDark &&
                        Math.Abs(pixels[index] - backgroundBlue) <= 14 &&
                        Math.Abs(pixels[index + 1] - backgroundGreen) <= 14 &&
                        Math.Abs(pixels[index + 2] - backgroundRed) <= 14)
                    {
                        pixels[index + 3] = 0;
                    }

                    if (pixels[index + 3] <= 8)
                    {
                        continue;
                    }

                    minX = Math.Min(minX, x);
                    minY = Math.Min(minY, y);
                    maxX = Math.Max(maxX, x);
                    maxY = Math.Max(maxY, y);
                }
            }

            if (maxX < minX || maxY < minY)
            {
                _normalizedUpgradeCache[path] = bitmap;
                return bitmap;
            }

            var transparent = BitmapSource.Create(
                source.PixelWidth,
                source.PixelHeight,
                source.DpiX,
                source.DpiY,
                PixelFormats.Bgra32,
                null,
                pixels,
                stride);
            var cropped = new CroppedBitmap(
                transparent,
                new System.Windows.Int32Rect(minX, minY, maxX - minX + 1, maxY - minY + 1));
            cropped.Freeze();
            _normalizedUpgradeCache[path] = cropped;
            return cropped;
        }

        private ImageSource LoadCargoIcon(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return null;
            }

            ImageSource cached;
            if (_cargoIconCache.TryGetValue(path, out cached))
            {
                return cached;
            }

            var bitmap = Load(path) as BitmapSource;
            if (bitmap == null)
            {
                return null;
            }

            var result = CargoIconRenderer.CreateOutlinedBitmap(bitmap);
            _cargoIconCache[path] = result;
            return result;
        }

        private ImageSource GetUnitIconForStateKey(string stateKey)
        {
            if (string.IsNullOrEmpty(stateKey) ||
                !stateKey.StartsWith("unit:", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            int unitId;
            if (!int.TryParse(stateKey.Substring("unit:".Length), out unitId))
            {
                return null;
            }

            var unit = Malco.Data.BwapiBroodWarTables.GetUnitTypeInfo(unitId);
            if (string.IsNullOrEmpty(unit.IconKey) || string.IsNullOrEmpty(_iconRoot))
            {
                return null;
            }

            var relativePath = unit.IconKey.Replace('/', Path.DirectorySeparatorChar) + ".png";
            return Load(Path.Combine(_iconRoot, "units", relativePath)) ??
                   Load(Path.Combine(_iconRoot, "construction", relativePath));
        }

        private void IndexUpgradeFiles()
        {
            if (string.IsNullOrEmpty(_iconRoot))
            {
                return;
            }

            var upgradeRoot = Path.Combine(_iconRoot, "upgrades");
            if (!Directory.Exists(upgradeRoot))
            {
                return;
            }

            foreach (var path in Directory.GetFiles(upgradeRoot, "*.png", SearchOption.AllDirectories))
            {
                var key = Normalize(Path.GetFileNameWithoutExtension(path));
                if (!_upgradeFiles.ContainsKey(key))
                {
                    _upgradeFiles[key] = path;
                }
            }
        }

        private ImageSource Load(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return null;
            }

            ImageSource image;
            if (_cache.TryGetValue(path, out image))
            {
                return image;
            }

            var bitmap = new BitmapImage();
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
            }
            bitmap.Freeze();
            _cache[path] = bitmap;
            return bitmap;
        }

        private static string FindIconRoot(string startDirectory)
        {
            if (!string.IsNullOrWhiteSpace(startDirectory))
            {
                var candidate = Path.Combine(startDirectory, "assets", "starcraft-icons");
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }
            }

            return string.Empty;
        }

        private static string Normalize(string value)
        {
            var chars = (value ?? string.Empty)
                .Trim()
                .ToLowerInvariant()
                .Replace('_', '-')
                .Select(ch => char.IsLetterOrDigit(ch) || ch == '-' ? ch : '-')
                .ToArray();
            return Regex.Replace(new string(chars), "-+", "-").Trim('-');
        }

        private static string TrimRacePrefix(string key)
        {
            foreach (var race in new[] { "terran-", "zerg-", "protoss-" })
            {
                if (key.StartsWith(race, StringComparison.OrdinalIgnoreCase))
                {
                    return key.Substring(race.Length);
                }
            }

            return key;
        }
    }
}
