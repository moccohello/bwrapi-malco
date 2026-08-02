using System;

namespace Malco.Presentation.Hud.Tiles
{
    internal struct TileMetrics
    {
        private const double TileMaxWidth = 38d;
        private const double TileMinWidth = 22d;
        private const double TileMaxHeight = 44d;
        private const double TileMinHeight = 28d;

        public double Width;
        public double Height;
        public double FrameWidth;
        public double FrameHeight;
        public double BadgeWidth;
        public double BadgeHeight;
        public double BadgeLeft;
        public double BadgeTop;
        public double BadgeFontSize;
        public double UpgradeBadgeFontSize;
        public double FallbackFontSize;
        public double IconMargin;
        public double Gap;

        public static TileMetrics FromWidth(double width, double gap)
        {
            var tileWidth = Math.Min(TileMaxWidth, Math.Max(TileMinWidth, width));
            var tileHeight = Math.Min(TileMaxHeight, Math.Max(TileMinHeight, tileWidth / (TileMaxWidth / TileMaxHeight)));
            var compact = tileWidth < 30d;
            var frameWidth = Math.Max(compact ? 18d : 24d, tileWidth - 4d);
            var frameHeight = Math.Max(compact ? 18d : 26d, tileHeight - 14d);
            var badgeWidth = Math.Max(compact ? 18d : 22d, Math.Min(38d, tileWidth - 10d));
            var badgeHeight = compact
                ? 11d
                : Math.Max(14d, Math.Min(19d, tileHeight * 0.3d));

            return new TileMetrics
            {
                Width = tileWidth,
                Height = tileHeight,
                FrameWidth = frameWidth,
                FrameHeight = frameHeight,
                BadgeWidth = badgeWidth,
                BadgeHeight = badgeHeight,
                BadgeLeft = Math.Max(2d, (tileWidth - badgeWidth) / 2d),
                BadgeTop = Math.Max(0d, tileHeight - badgeHeight - 1d),
                BadgeFontSize = Math.Max(8d, Math.Min(10d, tileWidth / 4d)),
                UpgradeBadgeFontSize = Math.Max(9d, Math.Min(16d, tileWidth / 3.2d)),
                FallbackFontSize = Math.Max(9d, Math.Min(16d, tileWidth / 3d)),
                IconMargin = tileWidth <= 46d ? 1d : 2d,
                Gap = gap
            };
        }
    }
}
