
using System;
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
    internal sealed partial class OverlaySceneViewController
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
        public bool ProjectionDemanded => _applicationController.ProjectionDemanded;
        public bool CommandsDemanded => _applicationController.CommandsDemanded;
        public long DemandEpoch => _applicationController.DemandEpoch;

        public void ResetForMissingTarget(FrozenSemanticSnapshot snapshot)
        {
            _hasAuthoritativeOutOfMatch = false;
            _latestSnapshot = snapshot ?? FrozenSemanticSnapshot.Freeze(new GameSnapshot());
            _latestCommands = CommandProjectionState.Unavailable(0, _latestSnapshot.WorkerStateStatus);
        }

        
        internal void UpdateChannelDemand(
            bool showBuildingRallyLines,
            bool showUnitCommandLines)
        {
            var showMineralWorkers = _view.IsFeatureEnabled(HudWidgetRegistry.MineralWorkers);
            var showGasWorkers = _view.IsFeatureEnabled(HudWidgetRegistry.GasWorkers);
            var needsCommands = showBuildingRallyLines || showUnitCommandLines;
            var preferences = _view.DisplayPreferences;
            var needsProjection = showMineralWorkers ||
                                  showGasWorkers ||
                                  needsCommands ||
                                  (preferences != null && preferences.HasUnitSpatialOverlays);
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
