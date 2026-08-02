using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using Malco.Data;

namespace Malco.Presentation.Spatial
{
    internal sealed partial class SpatialPresenter
    {
        private const double GasWorkerFontSize = 16.5d;
        private const double MineralWorkerFontSize = 15.5d;
        private static readonly SolidColorBrush UnitOverlayBackground =
            FrozenBrush(Color.FromArgb(84, 9, 16, 24));
        private static readonly SolidColorBrush TransportCargoOverlayBackground =
            FrozenBrush(Color.FromArgb(120, 8, 18, 32));
        private static readonly SolidColorBrush AbilityReadyFill =
            FrozenBrush(Color.FromArgb(153, 239, 42, 55));
        private static readonly SolidColorBrush AbilityReadyStroke =
            FrozenBrush(Colors.White);

        private readonly SpatialVisualTree _tree;
        private readonly SpatialVisualStyle _style;
        private readonly IconLocator _icons;
        private readonly SpatialIdentityReconciler _identity = new SpatialIdentityReconciler();
        private FrozenSemanticSnapshot _snapshot;
        private CommandProjectionState _commands;
        private string _sessionEpoch = string.Empty;
        private long _sessionGeneration = long.MinValue;
        private long _contentRevision;
        private long _positionedContentRevision = -1;
        private long _positionedProjectionGeneration = long.MinValue;
        private string _positionedProjectionEpoch = string.Empty;
        private int _positionedViewportMapX;
        private int _positionedViewportMapY;
        private bool _positionedProjectionUsable;
        private double _positionedWidth = -1d;
        private double _positionedHeight = -1d;
        private bool _positionedOriginalAspectRatio;
        private double _appliedVisualScale = double.NaN;
        private bool _contentInvalidated;

        public SpatialPresenter(SpatialVisualTree tree, SpatialVisualStyle style, IconLocator icons)
        {
            _tree = tree ?? throw new ArgumentNullException(nameof(tree));
            _style = style ?? throw new ArgumentNullException(nameof(style));
            _icons = icons;
        }

        public bool HasVisibleContent => _tree.HasVisuals;
        public bool HasActiveUnitOverlayMotion => _tree.HasActiveUnitOverlayMotion;

        public SpatialSlowApplyResult ResetSession(string sessionEpoch, long sessionGeneration)
        {
            var normalizedEpoch = sessionEpoch ?? string.Empty;
            if (_sessionGeneration == sessionGeneration &&
                string.Equals(_sessionEpoch, normalizedEpoch, StringComparison.Ordinal))
            {
                return new SpatialSlowApplyResult(false, 0, 0, 0, default);
            }

            _sessionEpoch = normalizedEpoch;
            _sessionGeneration = sessionGeneration;
            return ClearContentCore();
        }

        public SpatialSlowApplyResult ClearContent()
        {
            return ClearContentCore();
        }

        public SpatialSlowApplyResult ApplySlowState(in SpatialSlowState state)
        {
            var reset = ResetSession(state.SessionEpoch, state.SessionGeneration);
            _snapshot = state.Snapshot;
            _commands = state.Commands;
            var structuralChanged = reset.StructuralChanged;
            var creates = reset.Creates;
            var updates = reset.Updates;
            var removes = reset.Removes;
            var frameInvalidated = reset.FrameInvalidated;
            var canPresentContent = !state.Surface.IsEditor &&
                                    state.Snapshot != null &&
                                    state.Snapshot.IsInMatch;

            var semanticContentDirty =
                state.SemanticDirty || (_contentInvalidated && canPresentContent);
            if (semanticContentDirty || state.CommandDirty)
            {
                var structural = ReconcileContent(
                    state.Snapshot,
                    state.Commands,
                    state.Preferences,
                    state.Surface.IsEditor,
                    state.MonotonicTimestamp,
                    semanticContentDirty);
                _contentInvalidated = !canPresentContent;
                structuralChanged |= structural.StructuralChanged;
                creates += structural.Creates;
                updates += structural.Updates;
                removes += structural.Removes;
                frameInvalidated |= structural.FrameInvalidated;
                _contentRevision++;
            }

            if (state.SemanticDirty)
                updates += RefreshBadgeCounts(state.Snapshot, state.Surface.IsEditor);

            return new SpatialSlowApplyResult(
                structuralChanged,
                creates,
                updates,
                removes,
                default,
                frameInvalidated);
        }

        public SpatialFrameApplyResult ApplyCompositionFrame(in SpatialCompositionFrame frame)
        {
            var surface = frame.Surface;
            if (surface.IsEditor || !surface.IsGameplay || !surface.HasUsableTarget ||
                _snapshot == null || !_snapshot.IsInMatch)
            {
                _tree.SnapUnitOverlayMotions();
                return new SpatialFrameApplyResult(false, 0, HudClipAction.Unchanged);
            }

            var active = _tree.HasVisuals;
            if (!active)
            {
                return new SpatialFrameApplyResult(false, 0, HudClipAction.Unchanged);
            }

            if (frame.IsAuthoritativeClear || !frame.IsUsable ||
                frame.SessionGeneration != _sessionGeneration ||
                !string.Equals(frame.SessionEpoch, _sessionEpoch, StringComparison.Ordinal))
            {
                _tree.SnapUnitOverlayMotions();
                if (_positionedContentRevision == _contentRevision &&
                    !_positionedProjectionUsable &&
                    _positionedProjectionGeneration == frame.SessionGeneration &&
                    string.Equals(_positionedProjectionEpoch, frame.SessionEpoch, StringComparison.Ordinal))
                {
                    return new SpatialFrameApplyResult(true, 0, HudClipAction.Unchanged);
                }

                _tree.ClearClip();
                var visibilityWrites = _tree.SetVisualsVisibility(Visibility.Collapsed);
                StampFrame(frame);
                return new SpatialFrameApplyResult(true, visibilityWrites, HudClipAction.Clear);
            }

            if (_positionedContentRevision == _contentRevision &&
                !_tree.HasActiveUnitOverlayMotion &&
                _positionedProjectionUsable &&
                _positionedProjectionGeneration == frame.SessionGeneration &&
                string.Equals(_positionedProjectionEpoch, frame.SessionEpoch, StringComparison.Ordinal) &&
                _positionedViewportMapX == frame.ViewportMapX &&
                _positionedViewportMapY == frame.ViewportMapY &&
                Math.Abs(_positionedWidth - surface.Width) <= .5d &&
                Math.Abs(_positionedHeight - surface.Height) <= .5d &&
                _positionedOriginalAspectRatio == surface.OriginalAspectRatio)
            {
                return new SpatialFrameApplyResult(true, 0, HudClipAction.Unchanged);
            }

            SpatialProjection projection;
            GameRenderFrame renderFrame;
            if (!ProjectionCalculator.TryCreateProjection(
                frame.IsUsable,
                frame.ViewportMapX,
                frame.ViewportMapY,
                surface,
                out projection,
                out renderFrame))
            {
                _tree.ClearClip();
                _tree.SnapUnitOverlayMotions();
                var visibilityWrites = _tree.SetVisualsVisibility(Visibility.Collapsed);
                StampFrame(frame);
                return new SpatialFrameApplyResult(true, visibilityWrites, HudClipAction.Clear);
            }

            _tree.UpdateClip(renderFrame.GameplayRect);
            var positionWrites = 0;
            var presentationTimestamp = Stopwatch.GetTimestamp();
            var remeasureUnitOverlays = false;
            if (double.IsNaN(_appliedVisualScale) ||
                Math.Abs(_appliedVisualScale - projection.UiScale) > 0.000001d)
            {
                positionWrites += ApplyVisualScale(projection.UiScale);
                _appliedVisualScale = projection.UiScale;
                remeasureUnitOverlays = true;
            }
            for (var index = 0; index < _tree.RallyCount; index++)
                positionWrites += PositionRallyVisual(projection, _tree.GetRallyAt(index));
            for (var index = 0; index < _tree.GasCount; index++)
                positionWrites += PositionGasVisual(projection, _tree.GetGasAt(index));
            for (var index = 0; index < _tree.MineralCount; index++)
                positionWrites += PositionMineralVisual(projection, _tree.GetMineralAt(index));
            for (var index = 0; index < _tree.UnitOverlayCount; index++)
                positionWrites += PositionUnitOverlayVisual(
                    projection,
                    _tree.GetUnitOverlayAt(index),
                    presentationTimestamp,
                    remeasureUnitOverlays);
            StampFrame(frame);
            return new SpatialFrameApplyResult(
                true,
                positionWrites,
                HudClipAction.Set(ProjectionCalculator.BuildHudGameplayClip(surface, renderFrame)));
        }


        public HudClipAction ApplySurfaceClip(in SpatialSurfaceState surface)
        {
            _positionedContentRevision = -1;
            if (surface.IsEditor || !surface.IsGameplay)
            {
                _tree.ClearClip();
                return HudClipAction.Clear;
            }

            GameRenderFrame frame;
            if (!ProjectionCalculator.TryCreateGameRenderFrame(surface, out frame))
            {
                _tree.ClearClip();
                return HudClipAction.Clear;
            }

            _tree.UpdateClip(frame.GameplayRect);
            return HudClipAction.Set(ProjectionCalculator.BuildHudGameplayClip(surface, frame));
        }

        public void SetHostVisibility(Visibility visibility) => _tree.SetHostVisibility(visibility);

        public void ClearClip() => _tree.ClearClip();

        public void InvalidateSurface()
        {
            _positionedWidth = -1d;
            _positionedHeight = -1d;
        }

        private SpatialSlowApplyResult ClearContentCore()
        {
            var result = _tree.Clear();
            var hadContent = result.Changed || _snapshot != null || _commands != null;
            _identity.Clear();
            _snapshot = null;
            _commands = null;
            _contentInvalidated = true;
            if (hadContent)
            {
                _contentRevision++;
                InvalidateSurface();
            }
            return new SpatialSlowApplyResult(result.Changed, 0, 0, result.RemovedVisualCount, default);
        }

    }
}
