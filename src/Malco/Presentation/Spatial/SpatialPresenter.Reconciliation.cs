using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Media;
using Malco.Data;
using Malco.Models;
using WpfEllipse = System.Windows.Shapes.Ellipse;
using WpfLine = System.Windows.Shapes.Line;

namespace Malco.Presentation.Spatial
{
    internal sealed partial class SpatialPresenter
    {
        private SpatialSlowApplyResult ReconcileContent(
            FrozenSemanticSnapshot snapshot,
            CommandProjectionState commands,
            SpatialFeaturePreferences preferences,
            bool isEditor,
            long sampleTimestamp,
            bool semanticOverlaysDirty)
        {
            if (isEditor || snapshot == null || !snapshot.IsInMatch) return ClearContentCore();

            var lineEntries = IndexSpatialLines(FilterSpatialLines(BuildSpatialLines(snapshot, commands), preferences)).ToList();
            var linesById = lineEntries.ToDictionary(entry => entry.LineId, entry => entry.Point, StringComparer.Ordinal);
            var orderedGasGroups = preferences.ShowGasWorkers
                ? (snapshot.GasWorkerGroups ?? Array.Empty<GasWorkerGroup>()).Where(group => group != null).ToList()
                : new List<GasWorkerGroup>();
            var orderedMineralGroups = preferences.ShowMineralWorkers
                ? (snapshot.MineralWorkerGroups ?? Array.Empty<MineralWorkerGroup>()).Where(group => group != null).ToList()
                : new List<MineralWorkerGroup>();
            var gasById = orderedGasGroups.GroupBy(GasSpatialId)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            var mineralsById = orderedMineralGroups.GroupBy(MineralSpatialId)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            var unitOverlaysById = semanticOverlaysDirty
                ? BuildUnitOverlays(snapshot, preferences)
                : null;

            var lineChanges = _identity.ReconcileLines(linesById.Keys);
            var gasChanges = semanticOverlaysDirty
                ? _identity.ReconcileGas(gasById.Keys)
                : SpatialIdentityChanges.Unchanged;
            var mineralChanges = semanticOverlaysDirty
                ? _identity.ReconcileMinerals(mineralsById.Keys)
                : SpatialIdentityChanges.Unchanged;
            var creates = 0;
            var removes = 0;
            var updates = 0;
            var frameInvalidated = false;
            foreach (var id in lineChanges.Removed) { _tree.RemoveRally(id); removes++; }
            foreach (var id in gasChanges.Removed)
            {
                _tree.RemoveGas(id);
                removes++;
            }
            foreach (var id in mineralChanges.Removed)
            {
                _tree.RemoveMineral(id);
                removes++;
            }
            if (semanticOverlaysDirty)
            {
                foreach (var id in _tree.UnitOverlayIds
                             .Where(id => !unitOverlaysById.ContainsKey(id))
                             .ToList())
                {
                    _tree.RemoveUnitOverlay(id);
                    removes++;
                }
            }

            foreach (var pair in linesById)
            {
                RallySpatialVisual visual;
                if (_tree.TryGetRally(pair.Key, out visual))
                {
                    if (visual.UpdatePoint(pair.Value))
                    {
                        updates++;
                        frameInvalidated = true;
                    }
                }
                else { AddRally(pair.Key, pair.Value); creates++; }
            }
            if (semanticOverlaysDirty)
            {
                foreach (var pair in gasById)
                {
                    GasSpatialVisual visual;
                    if (_tree.TryGetGas(pair.Key, out visual))
                    {
                        if (visual.Group.MapX != pair.Value.MapX || visual.Group.MapY != pair.Value.MapY)
                        {
                            updates++;
                            frameInvalidated = true;
                        }
                        visual.Group = pair.Value;
                    }
                    else { AddGas(pair.Key, pair.Value); creates++; }
                }
            }
            if (semanticOverlaysDirty)
            {
                foreach (var pair in mineralsById)
                {
                    MineralSpatialVisual visual;
                    if (_tree.TryGetMineral(pair.Key, out visual))
                    {
                        if (visual.Group.MapX != pair.Value.MapX || visual.Group.MapY != pair.Value.MapY)
                        {
                            updates++;
                            frameInvalidated = true;
                        }
                        visual.Group = pair.Value;
                    }
                    else { AddMineral(pair.Key, pair.Value); creates++; }
                }
            }
            if (semanticOverlaysDirty)
            {
                foreach (var pair in unitOverlaysById)
                {
                    UnitOverlaySpatialVisual visual;
                    if (_tree.TryGetUnitOverlay(pair.Key, out visual))
                    {
                        if (visual.UpdateState(pair.Value.Item1, sampleTimestamp))
                        {
                            updates++;
                            frameInvalidated = true;
                        }
                        if (!string.Equals(visual.ContentKey, pair.Value.Item2, StringComparison.Ordinal))
                        {
                            ApplyUnitOverlayContent(visual.Badge, pair.Value.Item1, pair.Value.Item2);
                            visual.InvalidateLayoutSize();
                            visual.ContentKey = pair.Value.Item2;
                            updates++;
                            frameInvalidated = true;
                        }
                    }
                    else
                    {
                        AddUnitOverlay(pair.Key, pair.Value.Item1, pair.Value.Item2, sampleTimestamp);
                        creates++;
                    }
                }
            }

            var changed = lineChanges.HasIdentityChanges || gasChanges.HasIdentityChanges ||
                          mineralChanges.HasIdentityChanges || creates != 0 || removes != 0;
            var orderChanged = _tree.RestoreCanonicalOrder(
                lineEntries.Select(entry => entry.LineId).ToList(),
                orderedGasGroups.Select(GasSpatialId).Distinct(StringComparer.Ordinal).ToList(),
                orderedMineralGroups.Select(MineralSpatialId).Distinct(StringComparer.Ordinal).ToList());
            changed |= orderChanged;
            if (creates != 0)
            {
                _appliedVisualScale = double.NaN;
            }
            return new SpatialSlowApplyResult(
                changed,
                creates,
                updates,
                removes,
                default,
                frameInvalidated);
        }

        private void AddRally(string lineId, SpatialLine rally)
        {
            var color = SpatialVisualStyle.GetLineColor(rally);
            var line = new WpfLine
            {
                Stroke = new SolidColorBrush(color), StrokeThickness = 1d,
                StrokeDashArray = SpatialVisualStyle.GetLineDashArray(rally),
                Opacity = 0.44d
            };
            var ringSize = string.Equals(rally.Kind, "rally", StringComparison.OrdinalIgnoreCase) ? 8d : 7d;
            var ring = new WpfEllipse
            {
                Width = ringSize, Height = ringSize,
                Stroke = new SolidColorBrush(Color.FromArgb(255, color.R, color.G, color.B)),
                StrokeThickness = 1d,
                Fill = new SolidColorBrush(Color.FromArgb(48, color.R, color.G, color.B)),
                Opacity = 0.46d
            };
            WpfEllipse sourceRing = null;
            if (SpatialVisualStyle.IsPatrolLine(rally))
            {
                sourceRing = new WpfEllipse
                {
                    Width = 7d, Height = 7d,
                    Stroke = new SolidColorBrush(Color.FromArgb(255, color.R, color.G, color.B)),
                    StrokeThickness = 1d,
                    Fill = new SolidColorBrush(Color.FromArgb(48, color.R, color.G, color.B)),
                    Opacity = 0.42d
                };
            }
            _tree.AddRally(lineId, new RallySpatialVisual(lineId, rally, line, ring, sourceRing));
        }

        private static IEnumerable<SpatialLineEntry> IndexSpatialLines(IEnumerable<SpatialLine> points)
        {
            var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var point in (points ?? Array.Empty<SpatialLine>()).Where(point => point != null))
            {
                var baseId = string.Format(CultureInfo.InvariantCulture, "r:{0}:{1}:{2}:{3}",
                    point.SourceIdentity.Value, point.UnitId, point.Kind ?? string.Empty, point.Sequence);
                int occurrence; occurrences.TryGetValue(baseId, out occurrence); occurrences[baseId] = occurrence + 1;
                yield return new SpatialLineEntry(SpatialLineIdentity.BuildVisualId(point, occurrence), point);
            }
        }

        private static IEnumerable<SpatialLine> FilterSpatialLines(
            IEnumerable<SpatialLine> points, SpatialFeaturePreferences preferences) =>
            (points ?? Array.Empty<SpatialLine>()).Where(point => point != null).Where(point =>
                SpatialVisualStyle.IsRallyLine(point) ? preferences.ShowBuildingRallyLines : preferences.ShowUnitCommandLines).ToList();

        private static IEnumerable<SpatialLine> BuildSpatialLines(FrozenSemanticSnapshot snapshot, CommandProjectionState commands)
        {
            if (snapshot == null) return new List<SpatialLine>();
            var lines = new List<SpatialLine>();
            if (commands != null && commands.Lines != null) lines.AddRange(commands.Lines.Where(point => point != null));
            return lines;
        }
    }
}
