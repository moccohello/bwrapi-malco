using System;
using System.Diagnostics;
using System.Windows;
using Malco.Application.Projection;
using Malco.Data;
using Malco.Presentation.Hud.Units;
using Malco.Presentation.Hud.Upgrades;
using Malco.Presentation.Spatial;
using Malco.Settings.Contracts;
using Malco.Shell;

namespace Malco
{
    internal sealed partial class HudOverlayWindow
    {
        private void OnPresentationClock(object sender, EventArgs args)
        {
            _presentationScheduler.MarkClock();
        }

        internal void SetWidgetGameplayContent(string key, bool hasContent)
        {
            _hudVisualTree.SetGameplayContentAvailable(
                key,
                hasContent,
                _editorMode && _activeEditorPage == SettingsPage.Layout);
        }

        internal bool HasWidgetGameplayContent(string key)
        {
            return _hudVisualTree.HasGameplayContent(key);
        }

        internal void ApplyUnitAndBuildingPresenters(FrozenSemanticSnapshot snapshot, long sessionGeneration)
        {
            var input = new UnitHudPresentationInput(
                snapshot,
                sessionGeneration,
                _hudDisplayPreferences,
                _editorMode);
            SetWidgetGameplayContent(HudWidgetRegistry.Units, _unitsPresenter.Apply(input));
            SetWidgetGameplayContent(HudWidgetRegistry.Buildings, _buildingsPresenter.Apply(input));
        }

        private UpgradePresentationInput BuildUpgradePresentationInput(FrozenSemanticSnapshot snapshot, long sessionGeneration) =>
            new UpgradePresentationInput(snapshot, sessionGeneration, _hudDisplayPreferences, _editorMode);

        internal void ApplyUpgradePresenters(FrozenSemanticSnapshot snapshot, long sessionGeneration)
        {
            var availability = _upgradesPresenter.ApplySlowState(BuildUpgradePresentationInput(snapshot, sessionGeneration));
            SetWidgetGameplayContent(HudWidgetRegistry.Upgrades, availability.Completed);
            SetWidgetGameplayContent(HudWidgetRegistry.UpgradeCompletionWarnings, availability.Warnings);
            SetWidgetGameplayContent(HudWidgetRegistry.AvailableUpgrades, availability.Available);
        }

        internal void ApplyCompletedUpgradePresenters()
        {
            var availability = _upgradesPresenter.ApplyCompletedAndWarnings(
                BuildUpgradePresentationInput(_sceneViewController.LatestSnapshot, _scenePresenter.SessionGeneration));
            SetWidgetGameplayContent(HudWidgetRegistry.Upgrades, availability.Completed);
            SetWidgetGameplayContent(HudWidgetRegistry.UpgradeCompletionWarnings, availability.Warnings);
        }

        internal void ApplyAvailableUpgradePresenter()
        {
            SetWidgetGameplayContent(
                HudWidgetRegistry.AvailableUpgrades,
                _upgradesPresenter.ApplyAvailability(BuildUpgradePresentationInput(_sceneViewController.LatestSnapshot, _scenePresenter.SessionGeneration)));
        }

        private void OnRendering(object sender, EventArgs args)
        {
            var metricsProbe = _hudMetrics.BeginProbe();
            var activeSpatialPresentation = false;
            try
            {
                _shellController.ApplyPendingTargetGeometry();

                var projection = _projectionPresentation.ResolveLatest();
                var spatialFrame = new SpatialCompositionFrame(
                    projection.IsUsable,
                    projection.IsAuthoritativeClear,
                    projection.SessionEpoch,
                    projection.SessionGeneration,
                    projection.PresentationRevision,
                    projection.ViewportMapX,
                    projection.ViewportMapY,
                    BuildSpatialSurfaceState(_shellController.OriginalAspectRatio));
                var frameResult = _spatialPresenter.ApplyCompositionFrame(spatialFrame);
                activeSpatialPresentation = frameResult.ActivePresentation;
                ApplyHudClipAction(frameResult.HudClip);
                _hudMetrics.RecordPositionWrites(frameResult.PositionWrites);
            }
            finally
            {
                _hudMetrics.CompleteRendering(metricsProbe, activeSpatialPresentation);
            }
        }

        internal SpatialSurfaceState BuildSpatialSurfaceState(bool originalAspectRatio)
        {
            return new SpatialSurfaceState(
                _shellController.CurrentMode == OverlayRuntimeMode.Gameplay,
                _editorMode,
                _shellController.HasUsableTarget,
                _spatialCanvas.ActualWidth,
                _spatialCanvas.ActualHeight,
                originalAspectRatio);
        }

        internal void RecordSpatialResult(SpatialSlowApplyResult result)
        {
            _hudMetrics.RecordSpatialMutations(result.Creates, result.Updates, result.Removes);
            _hudMetrics.RecordPositionWrites(result.Frame.PositionWrites);
            ApplyHudClipAction(result.Frame.HudClip);
        }

        private void ApplyHudClipAction(HudClipAction action)
        {
            if (action.Kind == HudClipActionKind.Set)
            {
                _hudVisualTree.SetClip(action.Clip);
            }
            else if (action.Kind == HudClipActionKind.Clear)
            {
                _hudVisualTree.ClearClip();
            }
        }

        private void ClearSpatialClip()
        {
            _spatialPresenter.ClearClip();
        }

        private void RefreshSpatialGameplayClip(bool originalAspectRatio)
        {
            ApplyHudClipAction(_spatialPresenter.ApplySurfaceClip(BuildSpatialSurfaceState(originalAspectRatio)));
        }

        private void RefreshHudGameplayClip()
        {
            ApplyHudClipAction(_spatialPresenter.ApplySurfaceClip(
                BuildSpatialSurfaceState(_shellController.OriginalAspectRatio)));
        }

        private void ClearHudClip()
        {
            _hudVisualTree.ClearClip();
        }

        private void RefreshSpatialFeaturePresentation()
        {
            var showBuildingRallyLines = IsFeatureEnabled(HudWidgetRegistry.BuildingRallyLines);
            var showUnitCommandLines = IsFeatureEnabled(HudWidgetRegistry.UnitCommandLines);
            var showMineralWorkers = IsFeatureEnabled(HudWidgetRegistry.MineralWorkers);
            var showGasWorkers = IsFeatureEnabled(HudWidgetRegistry.GasWorkers);
            var commandsWereDemanded = _sceneViewController.CommandsDemanded;
            UpdateChannelDemand(_sceneViewController.LatestSnapshot, showBuildingRallyLines, showUnitCommandLines);
            var commands = !commandsWereDemanded && _sceneViewController.CommandsDemanded
                ? CommandProjectionState.Unavailable(
                    _scenePresenter.SessionGeneration,
                    "Waiting for fresh command projection",
                    sessionEpoch: _scenePresenter.SessionEpoch)
                : _sceneViewController.LatestCommands;
            var result = _spatialPresenter.ApplySlowState(new SpatialSlowState(
                _scenePresenter.SessionEpoch,
                _scenePresenter.SessionGeneration,
                _sceneViewController.LatestSnapshot,
                commands,
                new SpatialFeaturePreferences(
                    showBuildingRallyLines,
                    showUnitCommandLines,
                    showMineralWorkers,
                    showGasWorkers,
                    _hudDisplayPreferences),
                BuildSpatialSurfaceState(_shellController.OriginalAspectRatio),
                true,
                true,
                Stopwatch.GetTimestamp()));
            RecordSpatialResult(result);
            if (result.StructuralChanged || result.FrameInvalidated)
                _framePump.RequestFrame();
        }

        private void SetShellStatus(string status)
        {
            if (string.Equals(_lastShellStatusText, status, StringComparison.Ordinal))
            {
                return;
            }

            _lastShellStatusText = status;
            _editorStatus.Text = status ?? string.Empty;
        }
        private void OnOverlayDpiChanged(object sender, DpiChangedEventArgs args)
        {
            _shellController.NotifyDpiChanged();
        }

        internal void UpdateEditorPanelPlacement()
        {
            if (_editorPanel == null || _featurePanel == null)
            {
                return;
            }

            var height = ActualHeight > 0d ? ActualHeight : Height;
            var width = ActualWidth > 0d ? ActualWidth : Width;
            if (height <= 0d || width <= 0d)
            {
                return;
            }

            _editorPanel.Height = double.NaN;
            _editorPanel.Width = Math.Min(760d, Math.Max(240d, width - 32d));
            _editorPanel.VerticalAlignment = VerticalAlignment.Bottom;
            _editorPanel.Margin = new Thickness(16d, 0d, 16d, 16d);
            _layoutEditorView.UpdateResponsiveLayout(width);

            _featurePanel.Height = height;
            _featurePanel.Width = width;
            _featurePanel.Margin = new Thickness(0d);
            _featureSettingsView.UpdateResponsiveLayout(width);
        }

    }
}
