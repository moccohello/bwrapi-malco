using System;
using System.Windows;

namespace Malco.Presentation.Spatial
{
    internal struct SpatialProjection
    {
        private readonly double _minX;
        private readonly double _minY;
        private readonly double _scale;
        private readonly double _uiScale;
        private readonly double _screenX;
        private readonly double _screenY;
        private readonly double _maxScreenX;
        private readonly double _maxScreenY;

        public SpatialProjection(
            double minX,
            double minY,
            double scale,
            double uiScale,
            double screenX,
            double screenY,
            double maxScreenX,
            double maxScreenY)
        {
            _minX = minX;
            _minY = minY;
            _scale = scale;
            _uiScale = uiScale;
            _screenX = screenX;
            _screenY = screenY;
            _maxScreenX = maxScreenX;
            _maxScreenY = maxScreenY;
        }

        public double UiScale
        {
            get { return _uiScale; }
        }

        public double MapScale
        {
            get { return _scale; }
        }

        public Point ToScreen(double mapX, double mapY)
        {
            return new Point(
                _screenX + (mapX - _minX) * _scale,
                _screenY + (mapY - _minY) * _scale);
        }

        public bool IsRectInsideVisible(Rect rect)
        {
            return IsRectInsideVisible(rect, 0d);
        }

        public bool IsRectInsideVisible(Rect rect, double bottomInset)
        {
            return rect.Left >= _screenX &&
                   rect.Top >= _screenY &&
                   rect.Right <= _maxScreenX &&
                   rect.Bottom <= _maxScreenY - Math.Max(0d, bottomInset);
        }

        public bool TryClampRectInsideVisible(
            Rect desired,
            Point anchor,
            double horizontalAnchorMargin,
            double verticalAnchorMargin,
            out Rect placed)
        {
            placed = desired;
            var visibleWidth = _maxScreenX - _screenX;
            var visibleHeight = _maxScreenY - _screenY;
            if (desired.Width > visibleWidth || desired.Height > visibleHeight ||
                anchor.X < _screenX - horizontalAnchorMargin ||
                anchor.X > _maxScreenX + horizontalAnchorMargin ||
                anchor.Y < _screenY - verticalAnchorMargin ||
                anchor.Y > _maxScreenY + verticalAnchorMargin)
            {
                return false;
            }

            placed.X = Math.Max(_screenX, Math.Min(desired.X, _maxScreenX - desired.Width));
            placed.Y = Math.Max(_screenY, Math.Min(desired.Y, _maxScreenY - desired.Height));
            return true;
        }

        public bool TryClipLineToVisible(Point source, Point target, out Point clippedSource, out Point clippedTarget)
        {
            return TryClipLineToVisible(source, target, 0d, out clippedSource, out clippedTarget);
        }

        public bool TryClipLineToVisible(
            Point source,
            Point target,
            double bottomInset,
            out Point clippedSource,
            out Point clippedTarget)
        {
            clippedSource = source;
            clippedTarget = target;
            var dx = target.X - source.X;
            var dy = target.Y - source.Y;
            var start = 0d;
            var end = 1d;
            if (!ClipLineBoundary(-dx, source.X - _screenX, ref start, ref end) ||
                !ClipLineBoundary(dx, _maxScreenX - source.X, ref start, ref end) ||
                !ClipLineBoundary(-dy, source.Y - _screenY, ref start, ref end) ||
                !ClipLineBoundary(
                    dy,
                    _maxScreenY - Math.Max(0d, bottomInset) - source.Y,
                    ref start,
                    ref end))
            {
                return false;
            }

            clippedSource = new Point(source.X + start * dx, source.Y + start * dy);
            clippedTarget = new Point(source.X + end * dx, source.Y + end * dy);
            return true;
        }

        private static bool ClipLineBoundary(double direction, double distance, ref double start, ref double end)
        {
            if (Math.Abs(direction) < 0.000001d)
            {
                return distance >= 0d;
            }

            var ratio = distance / direction;
            if (direction < 0d)
            {
                if (ratio > end) return false;
                if (ratio > start) start = ratio;
            }
            else
            {
                if (ratio < start) return false;
                if (ratio < end) end = ratio;
            }

            return true;
        }
    }
}
