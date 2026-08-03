using System;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Threading;
using Malco.Configuration.Models;
using Malco.Data;
using Malco.Models;
using Malco.Presentation;
using Malco.Presentation.Spatial;
using Malco.Settings.Contracts;
using Malco.Settings.Controller;
using Malco.Settings.Views;
using Malco.Shell;
using Malco.Shell.Tray;

namespace Malco
{
    internal sealed partial class HudOverlayWindow
    {
        private void ApplyEditorMode(bool enabled)
        {
            if (enabled)
            {
                // 설정 전용 소형 창을 편집 크기로 확장하기 전에 버튼을 숨긴다.
                _settingsButton.Visibility = Visibility.Collapsed;
            }
            _editorMode = enabled;
            _settingsPersistence.SetEditorActive(enabled);
            if (!enabled)
            {
                _pendingSettingsRevision = 0L;
                _settingsStatusClock.Stop();
            }
            UpdatePresentationClockArming();
            _spatialPresenter.InvalidateSurface();
            if (_framePump != null) _framePump.RequestFrame();
            if (enabled)
            {
                ClearHudClip();
            }
            else
            {
                RefreshHudGameplayClip();
            }
            _editorPanel.Visibility = enabled && _activeEditorPage == SettingsPage.Layout
                ? Visibility.Visible
                : Visibility.Collapsed;
            _featurePanel.Visibility = enabled && _activeEditorPage == SettingsPage.Features
                ? Visibility.Visible
                : Visibility.Collapsed;
            if (enabled)
            {
                if (_sceneViewController.LatestSnapshot != null && _sceneViewController.LatestSnapshot.Race != Race.Unknown)
                {
                    _selectedTechTreeRace = _sceneViewController.LatestSnapshot.Race;
                }

                RefreshLayoutSamples();
                RenderEditorTab();
            }

            ApplyWidgetEditorChrome();
            ClampWidgets();

            RefreshVisibility();
        }

        private void FocusActiveEditorNavigation()
        {
            if (_activeEditorPage == SettingsPage.Features)
            {
                _featureSettingsView.FocusPrimaryControl();
                return;
            }

            HudWidgetView selectedWidget;
            if (_widgets.TryGetValue(_selectedWidgetKey, out selectedWidget) &&
                selectedWidget != null &&
                selectedWidget.Handle.MoveThumb.IsVisible &&
                selectedWidget.Handle.MoveThumb.Focus())
            {
                return;
            }
            _layoutEditorView.FocusPrimaryControl();
        }

        private void ToggleEditorMode()
        {
            if (_editorMode && !SaveLayoutIfNeeded())
            {
                return;
            }

            ApplyEditorMode(!_editorMode);
            _shellController.RefreshRuntimeMode();
            _presentationScheduler.MarkOverlayStateCommitted(_coordinator.Latest);
            UpdatePresentationClockArming();
            if (_editorMode)
            {
                Activate();
                Focus();
                FocusActiveEditorNavigation();
            }
            else
            {
                _shellController.ActivateTargetIfAvailable();
            }
        }

        private void OpenEditorMode()
        {
            ApplyEditorMode(true);
            _shellController.RefreshRuntimeMode();
            _presentationScheduler.MarkOverlayStateCommitted(_coordinator.Latest);
            Activate();
            Focus();
            FocusActiveEditorNavigation();
        }

        internal void HandleSettingsIntent(SettingsIntent intent)
        {
            if (_shutdownRequested) return;
            switch (intent.Kind)
            {
                case SettingsIntentKind.OpenFeatures:
                    _layoutEditorView.SelectEditorTab(SettingsPage.Features);
                    OpenEditorMode();
                    break;
                case SettingsIntentKind.OpenLayout:
                    _layoutEditorView.SelectEditorTab(SettingsPage.Layout);
                    OpenEditorMode();
                    break;
                case SettingsIntentKind.OpenTechTree:
                    _featureSettingsView.OpenFeature(HudWidgetRegistry.Upgrades);
                    _layoutEditorView.SelectEditorTab(SettingsPage.Features);
                    OpenEditorMode();
                    break;
                case SettingsIntentKind.ToggleEditor:
                    ToggleEditorMode();
                    break;
                case SettingsIntentKind.ReturnToGame:
                case SettingsIntentKind.CloseEditor:
                    if (_editorMode)
                    {
                        ToggleEditorMode();
                    }
                    break;
            }
        }

        void ITrayIntentSink.OpenSettings()
        {
            HandleSettingsIntent(new SettingsIntent(SettingsIntentKind.OpenFeatures));
        }

        void ITrayIntentSink.RequestQuit()
        {
            ShutdownOverlay();
        }

        bool ISettingsViewActions.EditorMode => _editorMode;
        SettingsPage ISettingsViewActions.ActiveEditorPage { get => _activeEditorPage; set => _activeEditorPage = value; }
        string ISettingsViewActions.SelectedWidgetKey { get => _selectedWidgetKey; set => _selectedWidgetKey = value; }
        Race ISettingsViewActions.SelectedTechTreeRace
        {
            get => _selectedTechTreeRace;
            set
            {
                if (_selectedTechTreeRace == value)
                {
                    return;
                }

                _selectedTechTreeRace = value;
                RefreshLayoutSamples();
            }
        }
        double ISettingsViewActions.ViewWidth => ActualWidth > 0d ? ActualWidth : Width;
        Dispatcher ISettingsViewActions.Dispatcher => Dispatcher;
        HudLayoutConfig ISettingsViewActions.Layout => _settingsController.Layout;
        bool ISettingsViewActions.HudTemporarilyHidden => _hudTemporarilyHidden;
        string ISettingsViewActions.ProgramVersion => ProgramVersion;
        SettingsEditResult ISettingsViewActions.ApplyEdit(SettingsEdit edit) => ApplySettingsEdit(edit);
        void ISettingsViewActions.Dispatch(SettingsIntent intent) => HandleSettingsIntent(intent);
        bool ISettingsViewActions.IsFeatureEnabled(string key) => IsFeatureEnabled(key);
        void ISettingsViewActions.SetWidgetEnabled(string key, bool enabled) => SetWidgetEnabled(key, enabled);
        void ISettingsViewActions.SelectWidget(string key)
        {
            _selectedWidgetKey = key;
            ApplyWidgetEditorChrome();
        }
        void ISettingsViewActions.ResetWidgetLayout(string key) => ResetWidgetLayout(key);
        void ISettingsViewActions.ResetAllWidgetLayouts() => ResetAllWidgetLayouts();
        void ISettingsViewActions.RetrySettingsSave() => RetrySettingsSave();
        void ISettingsViewActions.RefreshPresenterViews() => RefreshPresenterViews();
        void ISettingsViewActions.RefreshSpatialPresentation() => RefreshSpatialFeaturePresentation();
        void ISettingsViewActions.RefreshVisibility() => RefreshVisibility();
        void ISettingsViewActions.UpdateEditorPlacement() => UpdateEditorPanelPlacement();
        void ISettingsViewActions.RefreshEditorView()
        {
            RenderEditorTab();
            ApplyWidgetEditorChrome();
        }
        void ISettingsViewActions.FocusActiveEditorSurface() => FocusActiveEditorNavigation();
        void ISettingsViewActions.SetHudTemporarilyHidden(bool hidden) => SetHudTemporarilyHidden(hidden);

        bool IOverlaySceneViewPort.ShutdownRequested => _shutdownRequested;
        bool IOverlaySceneViewPort.EditorMode => _editorMode;
        HudDisplayPreferences IOverlaySceneViewPort.DisplayPreferences => _hudDisplayPreferences;
        bool IOverlaySceneViewPort.IsFeatureEnabled(string key) => IsFeatureEnabled(key);
        bool IOverlaySceneViewPort.HasWidgetGameplayContent(string key) => HasWidgetGameplayContent(key);
        void IOverlaySceneViewPort.SetWidgetGameplayContent(string key, bool content) => SetWidgetGameplayContent(key, content);
        void IOverlaySceneViewPort.UpdateSettingsButtonStatus(string message, FrozenSemanticSnapshot snapshot) => UpdateSettingsButtonStatus(message, snapshot);
        void IOverlaySceneViewPort.RecordSpatialResult(SpatialSlowApplyResult result) => RecordSpatialResult(result);
        SpatialSurfaceState IOverlaySceneViewPort.BuildSpatialSurfaceState(bool originalAspectRatio) => BuildSpatialSurfaceState(originalAspectRatio);
        void IOverlaySceneViewPort.RefreshVisibility() => RefreshVisibility();

        Dispatcher IOverlayShellViewPort.Dispatcher { get { return Dispatcher; } }
        bool IOverlayShellViewPort.IsOverlayPresented { get { return _overlayPresentationVisible; } }
        bool IOverlayShellViewPort.IsOverlayTopmost { get { return Topmost; } set { Topmost = value; } }
        double IOverlayShellViewPort.OverlayLeft { get { return Left; } }
        double IOverlayShellViewPort.OverlayTop { get { return Top; } }
        double IOverlayShellViewPort.OverlayWidth { get { return Width; } }
        double IOverlayShellViewPort.OverlayHeight { get { return Height; } }
        private void SetOverlayPresentation(bool presented)
        {
            if (_overlayPresentationVisible == presented) return;
            _overlayPresentationVisible = presented;
            if (_initialVisibilityComplete) Opacity = presented ? 1d : 0d;
        }

        private void CompleteInitialOverlayVisibility()
        {
            if (_initialVisibilityComplete) return;
            _initialVisibilityComplete = true;
            Opacity = _overlayPresentationVisible ? 1d : 0d;
        }

        void IOverlayShellViewPort.SetOverlayPresented(bool presented) =>
            SetOverlayPresentation(presented);
        void IOverlayShellViewPort.CompleteInitialVisibility() =>
            CompleteInitialOverlayVisibility();
        void IOverlayShellViewPort.ApplyShellBounds(Rect bounds, bool clampWidgets)
        {
            Left = bounds.Left;
            Top = bounds.Top;
            Width = bounds.Width;
            Height = bounds.Height;
            if (clampWidgets) ClampWidgets();
            UpdateEditorPanelPlacement();
        }
        void IOverlayShellViewPort.PositionSettingsButtonAtOrigin()
        {
            Canvas.SetLeft(_settingsButton, 0d);
            Canvas.SetTop(_settingsButton, 0d);
        }

        bool ISettingsShellPort.EditorMode { get { return _editorMode; } }
        bool ISettingsShellPort.ShutdownRequested { get { return _shutdownRequested; } }
        bool ISettingsShellPort.ResourcesDisposed { get { return _resourcesDisposed; } }
        void ISettingsShellPort.RequestApplicationShutdown() { RequestApplicationShutdown(); }
        void ISettingsShellPort.SetShellStatus(string status) { SetShellStatus(status); }
        void ISettingsShellPort.SetShellHelpText(string message)
        {
            AutomationProperties.SetHelpText(this, message ?? string.Empty);
        }
        void ISettingsShellPort.ReportWindowEventFallback() { _trayController.ReportWindowEventFallback(); }
        void ISettingsShellPort.ReportHotkeyUnavailable() { _trayController.ReportHotkeyUnavailable(); }

        OverlayRuntimeMode IShellPresentationPort.DesiredRuntimeMode
        {
            get { return ResolveRuntimeMode(_sceneViewController.LatestSnapshot); }
        }

        void IShellPresentationPort.InvalidateSpatialSurface()
        {
            _spatialPresenter.InvalidateSurface();
            if (_framePump != null) _framePump.RequestFrame();
        }

        void IShellPresentationPort.ResetForMissingTarget(string message)
        {
            var snapshot = FrozenSemanticSnapshot.Freeze(
                GameSnapshotFactory.NotReady(message));
            _sceneViewController.ResetForMissingTarget(snapshot);
            _workersPresenter.ResetSession(_scenePresenter.SessionGeneration);
            _unitsPresenter.ResetSession(_scenePresenter.SessionGeneration);
            _buildingsPresenter.ResetSession(_scenePresenter.SessionGeneration);
            SetWidgetGameplayContent(HudWidgetRegistry.Workers, false);
            SetWidgetGameplayContent(HudWidgetRegistry.Units, false);
            SetWidgetGameplayContent(HudWidgetRegistry.Buildings, false);
            _coordinator.ClearStableSnapshot(snapshot.WorkerStateStatus);
            SetShellStatus(snapshot.WorkerStateStatus);
        }

        void IShellPresentationPort.ApplyRuntimeVisualState(
            OverlayRuntimeMode mode,
            bool originalAspectRatio)
        {
            if (mode == OverlayRuntimeMode.Gameplay)
            {
                RefreshHudGameplayClip();
                RefreshSpatialGameplayClip(originalAspectRatio);
            }
            else
            {
                ClearHudClip();
                ClearSpatialClip();
            }
            UpdateEditorPanelPlacement();
            _settingsButton.Visibility = mode == OverlayRuntimeMode.SettingsOnly
                ? Visibility.Visible
                : Visibility.Collapsed;
            RefreshVisibility();
        }

        internal void ApplyWidgetEditorChrome()
        {
            var layoutEditing = _editorMode &&
                                _activeEditorPage == SettingsPage.Layout;
            _hudCanvas.Background = layoutEditing ? EditorHitSurfaceBrush : null;
            foreach (var widget in _widgets.Values)
            {
                widget.SetEditorMode(
                    layoutEditing,
                    !widget.Layout.Enabled,
                    string.Equals(widget.Key, _selectedWidgetKey, StringComparison.OrdinalIgnoreCase));
                _hudVisualTree.SetGameplayContentAvailable(
                    widget.Key,
                    _hudVisualTree.HasGameplayContent(widget.Key),
                    layoutEditing);
            }
        }

        internal bool IsFeatureEnabled(string key)
        {
            HudWidgetView widget;
            if (_widgets.TryGetValue(key, out widget))
            {
                return widget.Layout.Enabled;
            }

            var definition = HudWidgetRegistry.EditorFeatures().FirstOrDefault(candidate =>
                string.Equals(candidate.Key, key, StringComparison.OrdinalIgnoreCase));
            return _settingsController.Layout.GetOrCreate(
                key,
                definition != null ? definition.X : 0d,
                definition != null ? definition.Y : 0d,
                definition != null ? definition.Width : 1d,
                definition != null ? definition.Height : 1d,
                definition == null || definition.EnabledByDefault).Enabled;
        }
    }
}
