using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Malco.Models;

namespace Malco.Data
{
    internal static class SpatialLineIdentity
    {
        public static StableIdentity Create(string sourceIdentity, string kind, int sequence)
        {
            var key = (sourceIdentity ?? string.Empty) + ":" + (kind ?? string.Empty) + ":" +
                      sequence.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return StableIdentity.Create("line", key);
        }

        public static string BuildVisualId(SpatialLine line, int occurrence)
        {
            if (line == null)
            {
                return "line:null:" + occurrence.ToString(CultureInfo.InvariantCulture);
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "r:{0}:{1}:{2}:{3}:{4}",
                line.SourceIdentity.Value,
                line.UnitId,
                line.Kind ?? string.Empty,
                line.Sequence,
                occurrence);
        }

        public static string BuildContentKey(IEnumerable<SpatialLine> lines)
        {
            var builder = new StringBuilder();
            foreach (var line in (lines ?? Enumerable.Empty<SpatialLine>())
                .Where(line => line != null)
                .OrderBy(line => line.SourceIdentity)
                .ThenBy(line => line.Kind ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(line => line.Sequence))
            {
                builder.Append(BuildVisualId(line, 0)).Append(':')
                    .Append(line.SourceMapX.ToString(CultureInfo.InvariantCulture)).Append(':')
                    .Append(line.SourceMapY.ToString(CultureInfo.InvariantCulture)).Append(':')
                    .Append(line.TargetMapX.ToString(CultureInfo.InvariantCulture)).Append(':')
                    .Append(line.TargetMapY.ToString(CultureInfo.InvariantCulture)).Append('|');
            }

            return builder.ToString();
        }
    }
}
