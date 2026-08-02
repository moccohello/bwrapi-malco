using Malco.Models;

namespace Malco.Data
{
    internal sealed class SpatialLine
    {
        public SpatialLine(
            StableIdentity sourceIdentity,
            int unitId,
            string unitName,
            string kind,
            int sequence,
            int sourceMapX,
            int sourceMapY,
            int targetMapX,
            int targetMapY)
        {
            SourceIdentity = sourceIdentity;
            UnitId = unitId;
            UnitName = unitName ?? string.Empty;
            Kind = kind ?? string.Empty;
            Sequence = sequence;
            SourceMapX = sourceMapX;
            SourceMapY = sourceMapY;
            TargetMapX = targetMapX;
            TargetMapY = targetMapY;
        }

        public StableIdentity SourceIdentity { get; private set; }
        public int UnitId { get; private set; }
        public string UnitName { get; private set; }
        public string Kind { get; private set; }
        public int Sequence { get; private set; }
        public int SourceMapX { get; private set; }
        public int SourceMapY { get; private set; }
        public int TargetMapX { get; private set; }
        public int TargetMapY { get; private set; }

    }
}
