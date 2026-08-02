using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Malco.Models;

namespace Malco.Presentation.Spatial
{
    internal sealed class UnitOverlaySpatialVisual
    {
        private const double MaximumContinuousSampleSeconds = 0.25d;
        private const double MaximumAnimatedMapPixelsPerSecond = 256d;
        private const double PositionQuantizationAllowance = 8d;
        private const double PositionAnimationSeconds = 1d / 24d;
        private double _animationStartMapX;
        private double _animationStartMapY;
        private double _targetMapX;
        private double _targetMapY;
        private long _lastSampleTimestamp;
        private long _animationStartTimestamp;
        private long _animationDurationTicks;

        public UnitOverlaySpatialVisual(
            UnitSpatialState state,
            Border badge,
            string contentKey,
            long sampleTimestamp)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
            Badge = badge;
            ContentKey = contentKey;
            _animationStartMapX = state.MapX;
            _animationStartMapY = state.MapY;
            _targetMapX = state.MapX;
            _targetMapY = state.MapY;
            _lastSampleTimestamp = sampleTimestamp;
            _animationStartTimestamp = sampleTimestamp;
        }

        public UnitSpatialState State { get; private set; }
        public Border Badge { get; }
        public string ContentKey { get; set; }
        public bool HasActiveMotion { get; private set; }
        public bool NeedsLayoutMeasure { get; private set; } = true;
        public double LayoutWidth { get; private set; } = 9d;
        public double LayoutHeight { get; private set; } = 9d;

        public bool UpdateState(UnitSpatialState state, long sampleTimestamp)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            var changed = state.MapX != State.MapX || state.MapY != State.MapY;
            var sampleIntervalTicks = sampleTimestamp - _lastSampleTimestamp;
            var deltaX = state.MapX - State.MapX;
            var deltaY = state.MapY - State.MapY;
            double presentedMapX;
            double presentedMapY;
            ResolveAnimation(sampleTimestamp, out presentedMapX, out presentedMapY);
            State = state;
            _lastSampleTimestamp = sampleTimestamp;
            if (!changed)
            {
                return false;
            }

            if (sampleIntervalTicks <= 0 ||
                sampleIntervalTicks > Stopwatch.Frequency * MaximumContinuousSampleSeconds)
            {
                SnapTo(state.MapX, state.MapY, sampleTimestamp);
            }
            else
            {
                var sampleSeconds = sampleIntervalTicks / (double)Stopwatch.Frequency;
                var maximumContinuousDistance =
                    PositionQuantizationAllowance + MaximumAnimatedMapPixelsPerSecond * sampleSeconds;
                var distanceSquared = deltaX * (double)deltaX + deltaY * (double)deltaY;
                if (distanceSquared > maximumContinuousDistance * maximumContinuousDistance)
                {
                    SnapTo(state.MapX, state.MapY, sampleTimestamp);
                }
                else
                {
                    _animationStartMapX = presentedMapX;
                    _animationStartMapY = presentedMapY;
                    _targetMapX = state.MapX;
                    _targetMapY = state.MapY;
                    _animationStartTimestamp = sampleTimestamp;
                    _animationDurationTicks = Math.Max(
                        1L,
                        (long)Math.Ceiling(Stopwatch.Frequency * PositionAnimationSeconds));
                    HasActiveMotion = true;
                }
            }
            return changed;
        }

        public void InvalidateLayoutSize()
        {
            NeedsLayoutMeasure = true;
        }

        public void MeasureLayoutSize()
        {
            var visibility = Badge.Visibility;
            Badge.Visibility = Visibility.Visible;
            Badge.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            LayoutWidth = Math.Max(9d, Badge.DesiredSize.Width);
            LayoutHeight = Math.Max(9d, Badge.DesiredSize.Height);
            Badge.Visibility = visibility;
            NeedsLayoutMeasure = false;
        }

        public void ResolvePresentedPosition(long timestamp, out double mapX, out double mapY)
        {
            ResolveAnimation(timestamp, out mapX, out mapY);
        }

        private void ResolveAnimation(long timestamp, out double mapX, out double mapY)
        {
            if (_animationDurationTicks <= 0)
            {
                mapX = _targetMapX;
                mapY = _targetMapY;
                HasActiveMotion = false;
                return;
            }

            var elapsedTicks = Math.Max(0L, timestamp - _animationStartTimestamp);
            if (elapsedTicks >= _animationDurationTicks)
            {
                mapX = _targetMapX;
                mapY = _targetMapY;
                _animationStartMapX = _targetMapX;
                _animationStartMapY = _targetMapY;
                _animationDurationTicks = 0;
                HasActiveMotion = false;
                return;
            }

            var progress = elapsedTicks / (double)_animationDurationTicks;
            mapX = _animationStartMapX + (_targetMapX - _animationStartMapX) * progress;
            mapY = _animationStartMapY + (_targetMapY - _animationStartMapY) * progress;
            HasActiveMotion = true;
        }

        private void SnapTo(double mapX, double mapY, long timestamp)
        {
            _animationStartMapX = mapX;
            _animationStartMapY = mapY;
            _targetMapX = mapX;
            _targetMapY = mapY;
            _animationStartTimestamp = timestamp;
            _animationDurationTicks = 0;
            HasActiveMotion = false;
        }

        public void SnapMotion()
        {
            SnapTo(State.MapX, State.MapY, Stopwatch.GetTimestamp());
        }
    }
}
