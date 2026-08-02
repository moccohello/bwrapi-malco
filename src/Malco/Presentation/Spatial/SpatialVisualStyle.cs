using System;
using System.Windows.Media;
using Malco.Data;

namespace Malco.Presentation.Spatial
{
    internal sealed class SpatialVisualStyle
    {
        public SpatialVisualStyle(Brush chipBackground, Brush chipBorder, Brush text, Brush coral)
        {
            ChipBackground = chipBackground ?? throw new ArgumentNullException(nameof(chipBackground));
            ChipBorder = chipBorder ?? throw new ArgumentNullException(nameof(chipBorder));
            Text = text ?? throw new ArgumentNullException(nameof(text));
            Coral = coral ?? throw new ArgumentNullException(nameof(coral));
        }

        public Brush ChipBackground { get; }
        public Brush ChipBorder { get; }
        public Brush Text { get; }
        public Brush Coral { get; }

        public Brush GetGasBadgeBrush(int workerCount) => workerCount >= 0 && workerCount < 3 ? Coral : Text;

        public static Color GetLineColor(SpatialLine point)
        {
            if (IsPatrolLine(point)) return Color.FromArgb(225, 82, 155, 255);
            if (StartsWith(point, "attack")) return Color.FromArgb(255, 255, 53, 53);
            if (StartsWith(point, "resource")) return Color.FromArgb(255, 144, 238, 144);
            if (StartsWith(point, "spell")) return Color.FromArgb(255, 255, 216, 77);
            return Color.FromArgb(255, 255, 255, 255);
        }

        public static DoubleCollection GetLineDashArray(SpatialLine point)
        {
            if (StartsWith(point, "attack")) return new DoubleCollection { 5d, 2d };
            if (StartsWith(point, "rally")) return new DoubleCollection { 8d, 3d };
            return new DoubleCollection { 4d, 3d };
        }

        public static bool IsPatrolLine(SpatialLine point) => StartsWith(point, "patrol");
        public static bool IsRallyLine(SpatialLine point) => StartsWith(point, "rally");

        private static bool StartsWith(SpatialLine point, string value) =>
            point != null && !string.IsNullOrEmpty(point.Kind) &&
            point.Kind.StartsWith(value, StringComparison.OrdinalIgnoreCase);
    }
}
