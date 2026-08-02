
using System;
using System.Diagnostics;
using System.Linq;
using System.Windows.Threading;
using Malco.Game.Services;
using Malco.Presentation.Hud.Buildings;
using Malco.Presentation.Hud.Units;
using Malco.Presentation.Hud.Upgrades;
using Malco.Presentation.Hud.Workers;
using Malco.Application.Contracts;
using Malco.Application.Contracts.Input;
using Malco.Application.Contracts.Output;
using Malco.Application.Overlay;
using Malco.Application.Projection;
using Malco.Data;
using Malco.Models;
using Malco.Presentation.Hud;
using Malco.Presentation.Scheduling;
using Malco.Presentation.Spatial;
using Malco.Shell;

namespace Malco.Presentation
{
    internal sealed class OverlaySceneViewController
    {
        private readonly IOverlaySceneViewPort _view;
        private readonly GameCoordinator _coordinator;
        private readonly OverlayApplicationController _applicationController;
        private readonly IOverlayReadModelSource _readModelSource;
        private readonly ProjectionPresentationAdapter _projectionPresentation;
        private readonly WorkersPresenter _workersPresenter;
        private readonly UnitsPresenter _unitsPresenter;
        private readonly BuildingsPresenter _buildingsPresenter;
        private readonly UpgradesPresenter _upgradesPresenter;
        private readonly SpatialPresenter _spatialPresenter;
        private readonly OverlayScenePresenter _scenePresenter;
        private readonly OverlayHudMetrics _hudMetrics;
        private readonly OverlayShellController _shellController;
        private readonly DispatcherTimer _presentationClock;
        private readonly IOverlayFramePump _framePump;
        private bool _hasAuthoritativeOutOfMatch;
        private FrozenSemanticSnapshot _latestSnapshot = FrozenSemanticSnapshot.Freeze(new GameSnapshot());
        private CommandProjectionState _latestCommands = CommandProjectionState.Unavailable(0, "No command projection collected");
        private SemanticSnapshotState _latestSemanticState;
        private bool _hasProjectionControl;
        private ProjectionControlState _lastProjectionControl;

        public OverlaySceneViewController(
            IOverlaySceneViewPort view,
            GameCoordinator coordinator,
            OverlayApplicationController applicationController,
            IOverlayReadModelSource readModelSource,
            ProjectionPresentationAdapter projectionPresentation,
            WorkersPresenter workersPresenter,
            UnitsPresenter unitsPresenter,
            BuildingsPresenter buildingsPresenter,
            UpgradesPresenter upgradesPresenter,
            SpatialPresenter spatialPresenter,
            OverlayScenePresenter scenePresenter,
            OverlayHudMetrics hudMetrics,
            OverlayShellController shellController,
            DispatcherTimer presentationClock,
            IOverlayFramePump framePump)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _coordinator = coordinator;
            _applicationController = applicationController ?? throw new ArgumentNullException(nameof(applicationController));
            _readModelSource = readModelSource;
            _projectionPresentation = projectionPresentation;
            _workersPresenter = workersPresenter;
            _unitsPresenter = unitsPresenter;
            _buildingsPresenter = buildingsPresenter;
            _upgradesPresenter = upgradesPresenter;
            _spatialPresenter = spatialPresenter;
            _scenePresenter = scenePresenter;
            _hudMetrics = hudMetrics;
            _shellController = shellController;
            _presentationClock = presentationClock;
            _framePump = framePump;
        }

        public FrozenSemanticSnapshot LatestSnapshot => _latestSnapshot;
        public CommandProjectionState LatestCommands => _latestCommands;
        public SemanticSnapshotState LatestSemanticState => _latestSemanticState;
        public bool ProjectionDemanded => _applicationController.ProjectionDemanded;
        public bool CommandsDemanded => _applicationController.CommandsDemanded;
        public long DemandEpoch => _applicationController.DemandEpoch;

        public void ResetForMissingTarget(FrozenSemanticSnapshot snapshot)
        {
            _hasAuthoritativeOutOfMatch = false;
            _latestSnapshot = snapshot ?? FrozenSemanticSnapshot.Freeze(new GameSnapshot());
            _latestSemanticState = null;
            _latestCommands = CommandProjectionState.Unavailable(0, _latestSnapshot.WorkerStateStatus);
        }

        
        internal void UpdateChannelDemand(
            FrozenSemanticSnapshot snapshot,
            bool showBuildingRallyLines,
            bool showUnitCommandLines)
        {
            var showMineralWorkers = _view.IsFeatureEnabled(HudWidgetRegistry.MineralWorkers);
            var showGasWorkers = _view.IsFeatureEnabled(HudWidgetRegistry.GasWorkers);
            var hasResourceSpatial = snapshot != null && snapshot.IsInMatch &&
                                     (showGasWorkers && snapshot.GasWorkerGroups != null && snapshot.GasWorkerGroups.Count != 0 ||
                                      showMineralWorkers && snapshot.MineralWorkerGroups != null && snapshot.MineralWorkerGroups.Count != 0);
            var needsCommands = snapshot != null && snapshot.IsInMatch &&
                                (showBuildingRallyLines || showUnitCommandLines);
            var hasUnitSpatial = snapshot != null && snapshot.IsInMatch &&
                                 snapshot.UnitSpatialStates != null &&
                                 snapshot.UnitSpatialStates.Any(state => state != null &&
                                     ((_view.DisplayPreferences != null &&
                                       _view.DisplayPreferences.ShowTransportCargo &&
                                       state.Cargo != null && state.Cargo.Count != 0) ||
                                      (_view.DisplayPreferences != null &&
                                       !string.Equals(
                                           _view.DisplayPreferences.AbilityDisplayMode(state.UnitId),
                                           Malco.Configuration.Models.MalcoPreferenceValues.AbilityHidden,
                                           StringComparison.Ordinal))));
            var needsProjection = snapshot != null && snapshot.IsInMatch &&
                                  (hasResourceSpatial || needsCommands || hasUnitSpatial);
            _applicationController.SetChannelDemand(needsProjection, needsCommands);
            _framePump.RequestFrame();
            UpdateFramePumpArming();
        }

        internal void UpdatePresentationClockArming()
        {
            var shouldRun = !_view.ShutdownRequested &&
                            (_shellController.CurrentMode == OverlayRuntimeMode.Gameplay &&
                             _upgradesPresenter.NeedsClockRefresh);
            if (shouldRun)
            {
                if (!_presentationClock.IsEnabled) _presentationClock.Start();
            }
            else if (_presentationClock.IsEnabled)
            {
                _presentationClock.Stop();
            }
        }

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
                _latestSemanticState = semanticState;
                var commands = overlayState != null && overlayState.Commands != null
                    ? overlayState.Commands
                    : CommandProjectionState.Unavailable(0, "Command projection unavailable");
                var scene = _scenePresenter.Evaluate(semanticState, commands);
                var sessionGeneration = scene.SessionGeneration;
                var sessionEpoch = scene.SessionEpoch;
                if (scene.GenerationChanged || authoritativeClearEdge)
                {
                    _workersPresenter.ResetSession(sessionGeneration);
                    _unitsPresenter.ResetSession(sessionGeneration);
                    _buildingsPresenter.ResetSession(sessionGeneration);
                    _upgradesPresenter.ResetSession(sessionGeneration);
                    _view.SetWidgetGameplayContent(HudWidgetRegistry.Workers, false);
                    _view.SetWidgetGameplayContent(HudWidgetRegistry.Units, false);
                    _view.SetWidgetGameplayContent(HudWidgetRegistry.Buildings, false);
                    _view.SetWidgetGameplayContent(HudWidgetRegistry.Upgrades, false);
                    _view.SetWidgetGameplayContent(HudWidgetRegistry.UpgradeCompletionWarnings, false);
                    _view.SetWidgetGameplayContent(HudWidgetRegistry.AvailableUpgrades, false);
                    var spatialReset = scene.GenerationChanged
                        ? _spatialPresenter.ResetSession(sessionEpoch, sessionGeneration)
                        : _spatialPresenter.ClearContent();
                    spatialSemanticsChanged = spatialReset.StructuralChanged;
                    _view.RecordSpatialResult(spatialReset);
                }
                var snapshot = overlayState != null &&
                               overlayState.Semantic != null &&
                               overlayState.Semantic.Snapshot != null
                    ? overlayState.Semantic.Snapshot
                    : FrozenSemanticSnapshot.Freeze(new GameSnapshot());
                if (semanticState != null && snapshot != null &&
                    (semanticState.IsAuthoritativeOutOfMatch || snapshot.IsInMatch))
                {
                    _hasAuthoritativeOutOfMatch = semanticState.IsAuthoritativeOutOfMatch;
                }
                var viewport = overlayState != null ? overlayState.Viewport : null;
                var showBuildingRallyLines = _view.IsFeatureEnabled(HudWidgetRegistry.BuildingRallyLines);
                var showUnitCommandLines = _view.IsFeatureEnabled(HudWidgetRegistry.UnitCommandLines);
                var showMineralWorkers = _view.IsFeatureEnabled(HudWidgetRegistry.MineralWorkers);
                var showGasWorkers = _view.IsFeatureEnabled(HudWidgetRegistry.GasWorkers);
                UpdateChannelDemand(snapshot, showBuildingRallyLines, showUnitCommandLines);
                _latestSnapshot = snapshot;
                _latestCommands = commands;
                UpdateProjectionControl(new ProjectionControlState(
                    viewport != null ? viewport.Status : ProviderStatus.NotReady,
                    sessionEpoch,
                    sessionGeneration,
                    // Keep the control identity atomic. UpdateChannelDemand may
                    // have advanced the local receipt after this viewport was read.
                    viewport != null ? viewport.DemandEpoch : DemandEpoch,
                    ProjectionDemanded,
                    viewport != null && viewport.IsAuthoritativeClear,
                    viewport != null ? viewport.ClearReason : ProjectionClearReason.None,
                    new ContentRevision(viewport != null ? viewport.Revision : 0),
                    viewport != null ? viewport.Message : string.Empty));
                var semanticChanged = scene.SemanticChanged;
                var commandChanged = scene.CommandsChanged;
                _view.UpdateSettingsButtonStatus(
                    overlayState != null && overlayState.Semantic != null
                        ? overlayState.Semantic.Message
                        : string.Empty,
                    snapshot);
                _shellController.RefreshRuntimeMode();
                var runtimeMode = _shellController.CurrentMode;
                if (runtimeMode == OverlayRuntimeMode.SettingsOnly)
                {
                    _scenePresenter.Accept(semanticState, commands);
                    return;
                }
        
                if (semanticChanged)
                {
                    _view.SetWidgetGameplayContent(
                        HudWidgetRegistry.Workers,
                        _workersPresenter.ApplyWorkers(
                            snapshot,
                            sessionGeneration,
                            _view.DisplayPreferences));
                }
                if (semanticChanged)
                {
                    var unitInput = new UnitHudPresentationInput(
                        snapshot,
                        sessionGeneration,
                        _view.DisplayPreferences,
                        _view.EditorMode);
                    _view.SetWidgetGameplayContent(HudWidgetRegistry.Units, _unitsPresenter.Apply(unitInput));
                    _view.SetWidgetGameplayContent(HudWidgetRegistry.Buildings, _buildingsPresenter.Apply(unitInput));
                    var upgradeAvailability = _upgradesPresenter.ApplySlowState(new UpgradePresentationInput(
                        snapshot,
                        sessionGeneration,
                        _view.DisplayPreferences,
                        _view.EditorMode));
                    _view.SetWidgetGameplayContent(HudWidgetRegistry.Upgrades, upgradeAvailability.Completed);
                    _view.SetWidgetGameplayContent(HudWidgetRegistry.UpgradeCompletionWarnings, upgradeAvailability.Warnings);
                    _view.SetWidgetGameplayContent(HudWidgetRegistry.AvailableUpgrades, upgradeAvailability.Available);
                }
                if (clockDue && _upgradesPresenter.NeedsClockRefresh)
                {
                    _upgradesPresenter.AdvanceClock(DateTime.Now);
                }
                var originalAspectRatio = _shellController.OriginalAspectRatio;
                var spatialResult = _spatialPresenter.ApplySlowState(new SpatialSlowState(
                    sessionEpoch,
                    sessionGeneration,
                    snapshot,
                    commands,
                    new SpatialFeaturePreferences(
                        showBuildingRallyLines,
                        showUnitCommandLines,
                        showMineralWorkers,
                        showGasWorkers,
                        _view.DisplayPreferences),
                    _view.BuildSpatialSurfaceState(originalAspectRatio),
                    semanticChanged,
                    commandChanged,
                    Stopwatch.GetTimestamp()));
                spatialSemanticsChanged |= spatialResult.StructuralChanged;
                _view.RecordSpatialResult(spatialResult);
                if (spatialResult.StructuralChanged || spatialResult.FrameInvalidated)
                    _framePump.RequestFrame();
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

        internal void DrainPresentationClock()
        {
            if (_shellController.CurrentMode == OverlayRuntimeMode.Gameplay &&
                _upgradesPresenter.NeedsClockRefresh)
            {
                _upgradesPresenter.AdvanceClock(DateTime.Now);
            }
        }

        private void UpdateFramePumpArming()
        {
            var active = !_view.ShutdownRequested &&
                         ProjectionDemanded &&
                         _spatialPresenter.HasVisibleContent &&
                         _shellController.CurrentMode == OverlayRuntimeMode.Gameplay &&
                         _shellController.HasUsableTarget;
            _framePump.SetProjectionEnabled(active);
            _framePump.SetAnimationEnabled(active && _spatialPresenter.HasActiveUnitOverlayMotion);
        }

        public void RefreshFramePumpArming() => UpdateFramePumpArming();

        private void UpdateProjectionControl(in ProjectionControlState control)
        {
            var changed = !_hasProjectionControl ||
                          _lastProjectionControl.Status != control.Status ||
                          !string.Equals(
                              _lastProjectionControl.SessionEpoch,
                              control.SessionEpoch,
                              StringComparison.Ordinal) ||
                          _lastProjectionControl.SessionGeneration != control.SessionGeneration ||
                          _lastProjectionControl.DemandEpoch != control.DemandEpoch ||
                          _lastProjectionControl.IsDemanded != control.IsDemanded ||
                          _lastProjectionControl.IsAuthoritativeClear != control.IsAuthoritativeClear ||
                          _lastProjectionControl.ClearReason != control.ClearReason ||
                          _lastProjectionControl.ProjectionRevision.Value != control.ProjectionRevision.Value;
            _lastProjectionControl = control;
            _hasProjectionControl = true;
            _projectionPresentation.UpdateControl(control);
            if (changed) _framePump.RequestFrame();
        }
    }
}
