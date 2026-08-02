using System.Windows.Shapes;
using Malco.Data;

namespace Malco.Presentation.Spatial
{
    internal sealed class RallySpatialVisual
    {
        public RallySpatialVisual(string lineId, SpatialLine point, Line line, Ellipse ring, Ellipse sourceRing)
        {
            LineId = lineId;
            Point = point;
            Line = line;
            Ring = ring;
            SourceRing = sourceRing;
        }

        public string LineId { get; private set; }

        public SpatialLine Point { get; private set; }

        public Line Line { get; private set; }

        public Ellipse Ring { get; private set; }

        public Ellipse SourceRing { get; private set; }

        public bool UpdatePoint(SpatialLine point)
        {
            var changed = HasPresentationDifference(Point, point);
            Point = point;
            return changed;
        }

        private static bool HasPresentationDifference(SpatialLine current, SpatialLine next)
        {
            if (ReferenceEquals(current, next)) return false;
            if (current == null || next == null) return true;
            return current.SourceIdentity != next.SourceIdentity ||
                   current.UnitId != next.UnitId ||
                   !string.Equals(current.Kind, next.Kind, System.StringComparison.OrdinalIgnoreCase) ||
                   current.Sequence != next.Sequence ||
                   current.SourceMapX != next.SourceMapX ||
                   current.SourceMapY != next.SourceMapY ||
                   current.TargetMapX != next.TargetMapX ||
                   current.TargetMapY != next.TargetMapY;
        }
    }
}
