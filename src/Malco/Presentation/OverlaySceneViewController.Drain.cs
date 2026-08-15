using System;
using System.Diagnostics;
using Malco.Application.Contracts;
using Malco.Application.Contracts.Input;
using Malco.Application.Contracts.Output;
using Malco.Application.Overlay;
using Malco.Application.Projection;
using Malco.Data;
using Malco.Models;
using Malco.Presentation.Hud;
using Malco.Presentation.Hud.Units;
using Malco.Presentation.Hud.Upgrades;
using Malco.Presentation.Scheduling;
using Malco.Presentation.Spatial;
using Malco.Shell;

namespace Malco.Presentation
{
    internal sealed partial class OverlaySceneViewController
    {
        internal void DrainPresentation(PresentationDirtyMask dirty)
        {
            var metricsProbe = _hudMetrics.BeginProbe();
            var spatialSemanticsChanged = false;
            try
            {
                var clockDue = (dirty & PresentationDirtyMask.Clock) != 0;
                if (dirty == PresentationDirtyMask.Clock)
                {
                    DrainPresentationClock();
                    return;
                }

                var overlayState = _readModelSource.Latest;
                var semanticState = overlayState != null ? overlayState.Semantic : null;
                var authoritativeClearEdge = semanticState != null &&
                    semanticState.IsAuthoritativeOutOfMatch &&
                    !_hasAuthoritativeOutOfMatch;
                var commands = ResolveCommands(overlayState);
                var scene = _scenePresenter.Evaluate(semanticState, commands);
                ResetSceneIfNeeded(scene, authoritativeClearEdge, ref spatialSemanticsChanged);

                var snapshot = ResolveSnapshot(overlayState);
                TrackAuthoritativeOutOfMatch(semanticState, snapshot);
                var viewport = overlayState != null ? overlayState.Viewport : null;
                var showBuildingRallyLines = _view.IsFeatureEnabled(HudWidgetRegistry.BuildingRallyLines);
                var showUnitCommandLines = _view.IsFeatureEnabled(HudWidgetRegistry.UnitCommandLines);
                var showMineralWorkers = _view.IsFeatureEnabled(HudWidgetRegistry.MineralWorkers);
                var showGasWorkers = _view.IsFeatureEnabled(HudWidgetRegistry.GasWorkers);
                UpdateChannelDemand(showBuildingRallyLines, showUnitCommandLines);
                _latestSnapshot = snapshot;
                _latestCommands = commands;
                PublishProjectionControl(viewport, scene);

                _view.UpdateSettingsButtonStatus(
                    semanticState != null
                        ? semanticState.Message
                        : string.Empty,
                    snapshot);
                _shellController.RefreshRuntimeMode();
                if (_shellController.CurrentMode == OverlayRuntimeMode.SettingsOnly)
                {
                    _scenePresenter.Accept(semanticState, commands);
                    return;
                }

                ApplySemanticHud(scene, snapshot);
                if (clockDue && _upgradesPresenter.NeedsClockRefresh)
                {
                    _upgradesPresenter.AdvanceClock(DateTime.Now);
                }

                ApplySpatialPresentation(
                    scene,
                    snapshot,
                    commands,
                    showBuildingRallyLines,
                    showUnitCommandLines,
                    showMineralWorkers,
                    showGasWorkers,
                    ref spatialSemanticsChanged);
                _view.RefreshVisibility();
                _scenePresenter.Accept(semanticState, commands);
            }
            finally
            {
                UpdatePresentationClockArming();
                UpdateFramePumpArming();
                _hudMetrics.CompleteTick(
                    metricsProbe,
                    spatialSemanticsChanged,
                    metricsProbe.Enabled ? _coordinator.GetMetricsSnapshot() : default);
            }
        }

        private static CommandProjectionState ResolveCommands(OverlayReadModel overlayState)
        {
            return overlayState != null && overlayState.Commands != null
                ? overlayState.Commands
                : CommandProjectionState.Unavailable(0, "Command projection unavailable");
        }

        private void ResetSceneIfNeeded(
            in OverlaySceneRoutingDecision scene,
            bool authoritativeClearEdge,
            ref bool spatialSemanticsChanged)
        {
            if (!scene.GenerationChanged && !authoritativeClearEdge) return;

            _workersPresenter.ResetSession(scene.SessionGeneration);
            _unitsPresenter.ResetSession(scene.SessionGeneration);
            _buildingsPresenter.ResetSession(scene.SessionGeneration);
            _upgradesPresenter.ResetSession(scene.SessionGeneration);
            _view.SetWidgetGameplayContent(HudWidgetRegistry.Workers, false);
            _view.SetWidgetGameplayContent(HudWidgetRegistry.Units, false);
            _view.SetWidgetGameplayContent(HudWidgetRegistry.Buildings, false);
            _view.SetWidgetGameplayContent(HudWidgetRegistry.Upgrades, false);
            _view.SetWidgetGameplayContent(HudWidgetRegistry.UpgradeCompletionWarnings, false);
            _view.SetWidgetGameplayContent(HudWidgetRegistry.AvailableUpgrades, false);
            var spatialReset = scene.GenerationChanged
                ? _spatialPresenter.ResetSession(scene.SessionEpoch, scene.SessionGeneration)
                : _spatialPresenter.ClearContent();
            spatialSemanticsChanged = spatialReset.StructuralChanged;
            _view.RecordSpatialResult(spatialReset);
        }

        private static FrozenSemanticSnapshot ResolveSnapshot(OverlayReadModel overlayState)
        {
            return overlayState != null &&
                   overlayState.Semantic != null &&
                   overlayState.Semantic.Snapshot != null
                ? overlayState.Semantic.Snapshot
                : FrozenSemanticSnapshot.Freeze(new GameSnapshot());
        }

        private void TrackAuthoritativeOutOfMatch(
            SemanticSnapshotState semanticState,
            FrozenSemanticSnapshot snapshot)
        {
            if (semanticState != null && snapshot != null &&
                (semanticState.IsAuthoritativeOutOfMatch || snapshot.IsInMatch))
            {
                _hasAuthoritativeOutOfMatch = semanticState.IsAuthoritativeOutOfMatch;
            }
        }

        private void PublishProjectionControl(
            ViewportProjectionState viewport,
            in OverlaySceneRoutingDecision scene)
        {
            UpdateProjectionControl(new ProjectionControlState(
                viewport != null ? viewport.Status : ProviderStatus.NotReady,
                scene.SessionEpoch,
                scene.SessionGeneration,
                // Keep the control identity atomic. UpdateChannelDemand may
                // have advanced the local receipt after this viewport was read.
                viewport != null ? viewport.DemandEpoch : DemandEpoch,
                ProjectionDemanded,
                viewport != null && viewport.IsAuthoritativeClear,
                viewport != null ? viewport.ClearReason : ProjectionClearReason.None,
                new ContentRevision(viewport != null ? viewport.Revision : 0),
                viewport != null ? viewport.Message : string.Empty));
        }

        private void ApplySemanticHud(
            in OverlaySceneRoutingDecision scene,
            FrozenSemanticSnapshot snapshot)
        {
            if (!scene.SemanticChanged) return;

            _view.SetWidgetGameplayContent(
                HudWidgetRegistry.Workers,
                _workersPresenter.ApplyWorkers(
                    snapshot,
                    scene.SessionGeneration,
                    _view.DisplayPreferences));
            var unitInput = new UnitHudPresentationInput(
                snapshot,
                scene.SessionGeneration,
                _view.DisplayPreferences,
                _view.EditorMode);
            _view.SetWidgetGameplayContent(HudWidgetRegistry.Units, _unitsPresenter.Apply(unitInput));
            _view.SetWidgetGameplayContent(HudWidgetRegistry.Buildings, _buildingsPresenter.Apply(unitInput));
            var upgradeAvailability = _upgradesPresenter.ApplySlowState(new UpgradePresentationInput(
                snapshot,
                scene.SessionGeneration,
                _view.DisplayPreferences,
                _view.EditorMode));
            _view.SetWidgetGameplayContent(HudWidgetRegistry.Upgrades, upgradeAvailability.Completed);
            _view.SetWidgetGameplayContent(HudWidgetRegistry.UpgradeCompletionWarnings, upgradeAvailability.Warnings);
            _view.SetWidgetGameplayContent(HudWidgetRegistry.AvailableUpgrades, upgradeAvailability.Available);
        }

        private void ApplySpatialPresentation(
            in OverlaySceneRoutingDecision scene,
            FrozenSemanticSnapshot snapshot,
            CommandProjectionState commands,
            bool showBuildingRallyLines,
            bool showUnitCommandLines,
            bool showMineralWorkers,
            bool showGasWorkers,
            ref bool spatialSemanticsChanged)
        {
            var originalAspectRatio = _shellController.OriginalAspectRatio;
            var spatialResult = _spatialPresenter.ApplySlowState(new SpatialSlowState(
                scene.SessionEpoch,
                scene.SessionGeneration,
                snapshot,
                commands,
                new SpatialFeaturePreferences(
                    showBuildingRallyLines,
                    showUnitCommandLines,
                    showMineralWorkers,
                    showGasWorkers,
                    _view.DisplayPreferences),
                _view.BuildSpatialSurfaceState(originalAspectRatio),
                scene.SemanticChanged,
                scene.CommandsChanged,
                Stopwatch.GetTimestamp()));
            spatialSemanticsChanged |= spatialResult.StructuralChanged;
            _view.RecordSpatialResult(spatialResult);
            if (spatialResult.StructuralChanged || spatialResult.FrameInvalidated)
                _framePump.RequestFrame();
        }
    }
}
