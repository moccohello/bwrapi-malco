using Malco.Data;

namespace Malco.Presentation.Spatial
{
    internal sealed class SpatialLineEntry
    {
        public SpatialLineEntry(string lineId, SpatialLine point)
        {
            LineId = lineId;
            Point = point;
        }

        public string LineId { get; private set; }

        public SpatialLine Point { get; private set; }
    }
}
