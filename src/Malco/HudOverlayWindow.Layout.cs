using System;
using System.Windows;
using Malco.Settings.Contracts;
using Malco.Settings.Views;
using Malco.Shell;

namespace Malco
{
    internal sealed partial class HudOverlayWindow
    {
        private void ClampWidgets()
        {
            var scale = GetHudUiScale(_hudCanvas.ActualWidth, _hudCanvas.ActualHeight);
            foreach (var widget in _widgets.Values)
            {
                widget.ApplyBounds(_hudCanvas, scale);
            }

        }

        private static double GetHudUiScale(double canvasWidth, double canvasHeight)
        {
            if (canvasWidth <= 0d || canvasHeight <= 0d)
            {
                return 1d;
            }

            return Math.Max(
                HudMinimumScale,
                Math.Min(
                    HudMaximumScale,
                    Math.Min(canvasWidth / HudReferenceWidth, canvasHeight / HudReferenceHeight)));
        }

        private void OnHudCanvasSizeChanged(object sender, SizeChangedEventArgs args)
        {
            _spatialPresenter.InvalidateSurface();
            ClampWidgets();
            RefreshHudGameplayClip();
            RefreshSpatialGameplayClip(_shellController.OriginalAspectRatio);
            if (_framePump != null) _framePump.RequestFrame();
            if (_editorMode && _activeEditorPage == SettingsPage.Layout)
            {
                _layoutEditorView.RefreshLayoutEditorState();
            }
        }

        private void OnOverlaySizeChanged(object sender, SizeChangedEventArgs args)
        {
            UpdateEditorPanelPlacement();
        }

        internal void RefreshVisibility()
        {
            RefreshSurfaceVisibility();
            var layoutEditing = _shellController.CurrentMode == OverlayRuntimeMode.Editor &&
                                _editorMode &&
                                _activeEditorPage == SettingsPage.Layout;
            foreach (var widget in _widgets.Values)
            {
                var canShowGameplayWidget = _shellController.CurrentMode == OverlayRuntimeMode.Gameplay;
                var visible = layoutEditing ||
                              (canShowGameplayWidget &&
                              widget.Layout.Enabled &&
                              HasWidgetGameplayContent(widget.Key));
                widget.Root.Visibility = visible
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        private void RefreshSurfaceVisibility()
        {
            var gameplayHiddenForSession = _hudTemporarilyHidden &&
                                           _shellController.CurrentMode == OverlayRuntimeMode.Gameplay;
            var surfaceVisible = gameplayHiddenForSession
                ? Visibility.Collapsed
                : Visibility.Visible;
            _hudCanvas.Visibility = surfaceVisible;
            _spatialPresenter.SetHostVisibility((_shellController.CurrentMode == OverlayRuntimeMode.Gameplay &&
                                                 !gameplayHiddenForSession) ||
                                                _shellController.CurrentMode == OverlayRuntimeMode.Editor
                ? Visibility.Visible
                : Visibility.Collapsed);
        }

        private void SetHudTemporarilyHidden(bool hidden)
        {
            if (_hudTemporarilyHidden == hidden)
            {
                return;
            }

            _hudTemporarilyHidden = hidden;
            _spatialPresenter.InvalidateSurface();
            RefreshVisibility();
            if (_framePump != null) _framePump.RequestFrame();
            _featureSettingsView.RefreshTemporaryHudState();
        }

        internal void SetWidgetEnabled(string key, bool enabled)
        {
            if (!ApplySettingsEdit(SettingsEdit.SetWidgetEnabled(key, enabled)).Changed)
            {
                return;
            }

            HudWidgetView widget;
            if (!_widgets.TryGetValue(key, out widget))
            {
                RefreshSpatialFeaturePresentation();
                return;
            }

            widget.ReplaceLayout(_settingsController.CaptureWidgetLayout(key));

            if (enabled && string.Equals(
                    key,
                    HudWidgetRegistry.AvailableUpgrades,
                    StringComparison.OrdinalIgnoreCase))
            {
                ApplyAvailableUpgradePresenter();
            }

            ApplyWidgetEditorChrome();
            RefreshVisibility();
            _layoutEditorView.RefreshLayoutEditorState();
        }

        private void ResetWidgetLayout(string key)
        {
            ApplyLayoutReset(SettingsEdit.ResetWidgetBounds(key));
        }

        private void ResetAllWidgetLayouts()
        {
            ApplyLayoutReset(SettingsEdit.ResetAllWidgetBounds());
        }

        private void ApplyLayoutReset(SettingsEdit edit)
        {
            if (!ApplySettingsEdit(edit).Changed)
            {
                return;
            }
            foreach (var widget in _widgets.Values)
            {
                widget.ReplaceLayout(_settingsController.CaptureWidgetLayout(widget.Key));
            }
            ClampWidgets();
            ApplyWidgetEditorChrome();
            RefreshVisibility();
            _layoutEditorView.RefreshLayoutEditorState();
        }

        private void OnWidgetLayoutChanged(object sender, EventArgs args)
        {
            var widget = sender as HudWidgetView;
            if (widget != null)
            {
                var layout = widget.Layout;
                ApplySettingsEdit(SettingsEdit.SetWidgetBounds(
                    widget.Key,
                    new WidgetBoundsValue(
                        layout.X,
                        layout.Y,
                        layout.Width,
                        layout.Height,
                        layout.HasRelativeBounds,
                        layout.XRatio,
                        layout.YRatio,
                        layout.WidthRatio,
                        layout.HeightRatio)));
            }
            RefreshVisibility();
            if (_editorMode && _activeEditorPage == SettingsPage.Layout)
            {
                _layoutEditorView.RefreshLayoutEditorState();
            }
        }

    }
}
