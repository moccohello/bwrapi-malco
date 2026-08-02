using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfLine = System.Windows.Shapes.Line;

namespace Malco.Presentation.Spatial
{
    internal sealed partial class SpatialPresenter
    {
        private void StampFrame(in SpatialCompositionFrame frame)
        {
            _positionedContentRevision = _contentRevision;
            _positionedProjectionEpoch = frame.SessionEpoch ?? string.Empty;
            _positionedProjectionGeneration = frame.SessionGeneration;
            _positionedViewportMapX = frame.ViewportMapX;
            _positionedViewportMapY = frame.ViewportMapY;
            _positionedProjectionUsable = frame.IsUsable;
            _positionedWidth = frame.Surface.Width;
            _positionedHeight = frame.Surface.Height;
            _positionedOriginalAspectRatio = frame.Surface.OriginalAspectRatio;
        }

        private int ApplyVisualScale(double scale)
        {
            var writes = 0;
            for (var index = 0; index < _tree.RallyCount; index++)
            {
                var rally = _tree.GetRallyAt(index);
                var size = string.Equals(rally.Point.Kind, "rally", StringComparison.OrdinalIgnoreCase) ? 8d * scale : 7d * scale;
                writes += SetStrokeThickness(rally.Line, 1d * scale);
                writes += SetWidth(rally.Ring, size);
                writes += SetHeight(rally.Ring, size);
                writes += SetStrokeThickness(rally.Ring, 1d * scale);
                if (rally.SourceRing != null)
                {
                    writes += SetWidth(rally.SourceRing, 7d * scale);
                    writes += SetHeight(rally.SourceRing, 7d * scale);
                    writes += SetStrokeThickness(rally.SourceRing, 1d * scale);
                }
            }
            for (var index = 0; index < _tree.GasCount; index++)
            {
                var gas = _tree.GetGasAt(index);
                writes += SetWidth(gas.Badge, 54d * scale);
                writes += SetHeight(gas.Badge, 30d * scale);
                writes += SetFontSize(gas.Text, GasWorkerFontSize * scale);
            }
            for (var index = 0; index < _tree.MineralCount; index++)
            {
                var mineral = _tree.GetMineralAt(index);
                writes += SetWidth(mineral.Badge, 56d * scale);
                writes += SetHeight(mineral.Badge, 26d * scale);
                writes += SetFontSize(mineral.Text, MineralWorkerFontSize * scale);
            }
            for (var index = 0; index < _tree.UnitOverlayCount; index++)
            {
                var visual = _tree.GetUnitOverlayAt(index);
                visual.Badge.LayoutTransform = new ScaleTransform(scale, scale);
                visual.InvalidateLayoutSize();
                writes++;
            }
            return writes;
        }

        private static int PositionGasVisual(SpatialProjection projection, GasSpatialVisual gas)
        {
            var point = projection.ToScreen(gas.Group.MapX, gas.Group.MapY);
            var bounds = new Rect(point.X - gas.Badge.Width / 2d, point.Y - gas.Badge.Height - 12d * projection.UiScale,
                gas.Badge.Width, gas.Badge.Height);
            if (!projection.TryClampRectInsideVisible(
                    bounds,
                    point,
                    64d * projection.MapScale,
                    32d * projection.MapScale,
                    out bounds))
                return SetVisibility(gas.Badge, Visibility.Collapsed);
            return SetCanvasLeft(gas.Badge, bounds.Left) + SetCanvasTop(gas.Badge, bounds.Top) +
                   SetVisibility(gas.Badge, Visibility.Visible);
        }

        private static int PositionMineralVisual(SpatialProjection projection, MineralSpatialVisual mineral)
        {
            var point = projection.ToScreen(mineral.Group.MapX, mineral.Group.MapY);
            var bounds = new Rect(point.X - mineral.Badge.Width / 2d, point.Y - mineral.Badge.Height - 16d * projection.UiScale,
                mineral.Badge.Width, mineral.Badge.Height);
            if (!projection.TryClampRectInsideVisible(
                    bounds,
                    point,
                    64d * projection.MapScale,
                    48d * projection.MapScale,
                    out bounds))
                return SetVisibility(mineral.Badge, Visibility.Collapsed);
            return SetCanvasLeft(mineral.Badge, bounds.Left) + SetCanvasTop(mineral.Badge, bounds.Top) +
                   SetVisibility(mineral.Badge, Visibility.Visible);
        }

        private static int PositionUnitOverlayVisual(
            SpatialProjection projection,
            UnitOverlaySpatialVisual visual,
            long presentationTimestamp,
            bool remeasure)
        {
            if (remeasure || visual.NeedsLayoutMeasure)
                visual.MeasureLayoutSize();
            var width = visual.LayoutWidth;
            var height = visual.LayoutHeight;
            double mapX;
            double mapY;
            visual.ResolvePresentedPosition(presentationTimestamp, out mapX, out mapY);
            var point = projection.ToScreen(mapX, mapY);
            var bounds = new Rect(
                point.X - width / 2d,
                point.Y - height / 2d,
                width,
                height);
            if (!projection.TryClampRectInsideVisible(
                    bounds,
                    point,
                    48d * projection.MapScale,
                    40d * projection.MapScale,
                    out bounds))
                return SetVisibility(visual.Badge, Visibility.Collapsed);
            return SetCanvasLeft(visual.Badge, bounds.Left) +
                   SetCanvasTop(visual.Badge, bounds.Top) +
                   SetVisibility(visual.Badge, Visibility.Visible);
        }

        private static int PositionRallyVisual(SpatialProjection projection, RallySpatialVisual rally)
        {
            var commandBottomInset = 2d * projection.MapScale;
            if (!HasDistinctEndpoints(
                    rally.Point.SourceMapX,
                    rally.Point.SourceMapY,
                    rally.Point.TargetMapX,
                    rally.Point.TargetMapY))
            {
                var marker = projection.ToScreen(rally.Point.TargetMapX, rally.Point.TargetMapY);
                var bounds = CenteredRect(marker, rally.Ring.Width, rally.Ring.Height);
                var writes = SetVisibility(rally.Line, Visibility.Collapsed);
                writes += SetVisibility(rally.Ring,
                    projection.IsRectInsideVisible(bounds, commandBottomInset)
                        ? Visibility.Visible
                        : Visibility.Collapsed);
                if (rally.SourceRing != null) writes += SetVisibility(rally.SourceRing, Visibility.Collapsed);
                writes += SetCanvasLeft(rally.Ring, bounds.Left);
                writes += SetCanvasTop(rally.Ring, bounds.Top);
                return writes;
            }
            var source = projection.ToScreen(rally.Point.SourceMapX, rally.Point.SourceMapY);
            var target = projection.ToScreen(rally.Point.TargetMapX, rally.Point.TargetMapY);
            Point clippedSource; Point clippedTarget;
            var visible = projection.TryClipLineToVisible(
                source,
                target,
                commandBottomInset,
                out clippedSource,
                out clippedTarget);
            var visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            var positionWrites = SetVisibility(rally.Line, visibility);
            var targetBounds = CenteredRect(target, rally.Ring.Width, rally.Ring.Height);
            positionWrites += SetVisibility(rally.Ring,
                visible && projection.IsRectInsideVisible(targetBounds, commandBottomInset)
                    ? Visibility.Visible
                    : Visibility.Collapsed);
            if (rally.SourceRing != null)
            {
                var sourceBounds = CenteredRect(source, rally.SourceRing.Width, rally.SourceRing.Height);
                positionWrites += SetVisibility(rally.SourceRing,
                    visible && projection.IsRectInsideVisible(sourceBounds, commandBottomInset)
                        ? Visibility.Visible
                        : Visibility.Collapsed);
            }
            if (!visible) return positionWrites;
            positionWrites += SetLineCoordinates(rally.Line, clippedSource, clippedTarget);
            if (rally.SourceRing != null)
            {
                positionWrites += SetCanvasLeft(rally.SourceRing, source.X - rally.SourceRing.Width / 2d);
                positionWrites += SetCanvasTop(rally.SourceRing, source.Y - rally.SourceRing.Height / 2d);
            }
            positionWrites += SetCanvasLeft(rally.Ring, targetBounds.Left);
            positionWrites += SetCanvasTop(rally.Ring, targetBounds.Top);
            return positionWrites;
        }

        private static int SetWidth(FrameworkElement element, double value)
        {
            if (AreEqual(element.Width, value)) return 0;
            element.Width = value;
            return 1;
        }

        private static int SetHeight(FrameworkElement element, double value)
        {
            if (AreEqual(element.Height, value)) return 0;
            element.Height = value;
            return 1;
        }

        private static int SetStrokeThickness(System.Windows.Shapes.Shape shape, double value)
        {
            if (AreEqual(shape.StrokeThickness, value)) return 0;
            shape.StrokeThickness = value;
            return 1;
        }

        private static int SetFontSize(TextBlock text, double value)
        {
            if (AreEqual(text.FontSize, value)) return 0;
            text.FontSize = value;
            return 1;
        }

        private static int SetVisibility(UIElement element, Visibility value)
        {
            if (element.Visibility == value) return 0;
            element.Visibility = value;
            return 1;
        }

        private static int SetCanvasLeft(UIElement element, double value)
        {
            if (AreEqual(Canvas.GetLeft(element), value)) return 0;
            Canvas.SetLeft(element, value);
            return 1;
        }

        private static int SetCanvasTop(UIElement element, double value)
        {
            if (AreEqual(Canvas.GetTop(element), value)) return 0;
            Canvas.SetTop(element, value);
            return 1;
        }

        private static int SetLineCoordinates(WpfLine line, Point source, Point target)
        {
            var writes = 0;
            if (!AreEqual(line.X1, source.X)) { line.X1 = source.X; writes++; }
            if (!AreEqual(line.Y1, source.Y)) { line.Y1 = source.Y; writes++; }
            if (!AreEqual(line.X2, target.X)) { line.X2 = target.X; writes++; }
            if (!AreEqual(line.Y2, target.Y)) { line.Y2 = target.Y; writes++; }
            return writes;
        }

        private static bool AreEqual(double left, double right)
        {
            return left.Equals(right) || Math.Abs(left - right) <= 0.000001d;
        }

        private static Rect CenteredRect(Point center, double width, double height) =>
            new Rect(center.X - width / 2d, center.Y - height / 2d, width, height);
        private static bool HasDistinctEndpoints(int sourceX, int sourceY, int targetX, int targetY) =>
            sourceX != targetX || sourceY != targetY;
    }
}
