using System;
using System.ComponentModel;
using System.Windows.Input;
using System.Windows.Interop;
using Malco.Settings.Contracts;

namespace Malco
{
    internal sealed partial class HudOverlayWindow
    {
        internal void RequestApplicationShutdown()
        {
            ShutdownOverlay();
        }

        private void ShutdownOverlay()
        {
            if (_shutdownRequested && !_shutdownBlocked)
            {
                return;
            }

            if (!_shutdownPreparationComplete)
            {
                _settingsPersistence.SetEditorActive(false);
                var settingsFlush = _settingsPersistence.TryFlush(SettingsFlushReason.Shutdown);
                PresentSettingsFlushResult(settingsFlush);
                if (!settingsFlush.ShouldContinueShutdown)
                {
                    _shutdownBlocked = true;
                    OpenEditorMode();
                    if (_activeEditorPage == SettingsPage.Layout)
                    {
                        _layoutEditorView.FocusSaveRecovery();
                    }
                    else
                    {
                        _featureSettingsView.FocusSaveRecovery();
                    }
                    return;
                }
            }

            PrepareShutdownOnce();
            var shutdown = _shutdownController.TryStopApplication(_coordinator);
            if (!shutdown.IsComplete)
            {
                _shutdownBlocked = true;
                _editorStatus.Text = shutdown.Message;
                _trayController.ReportShutdownBlocked(shutdown.Message);
                return;
            }

            _shutdownBlocked = false;
            CompleteRuntimeTeardown();
            Close();
            var app = System.Windows.Application.Current;
            if (app != null && !app.Dispatcher.HasShutdownStarted)
            {
                app.Shutdown(0);
            }
        }

        private void PrepareShutdownOnce()
        {
            if (_shutdownPreparationComplete) return;
            _shutdownRequested = true;
            _shutdownPreparationComplete = true;
            _shellController.PrepareForRuntimeShutdown();
            _projectionCommitSubscription.Dispose();
            _framePump.Stop();
            _coordinator.UnregisterStateCommitSink(_presentationScheduler);
            if (_telemetry != null) _coordinator.UnregisterStateCommitSink(_telemetry);
            _presentationScheduler.Stop();
            _presentationClock.Stop();
            if (IsVisible) Hide();
        }

        private void OnSourceInitialized(object sender, EventArgs args)
        {
            _shellController.Initialize(new WindowInteropHelper(this).Handle);
        }
        private void OnPreviewKeyDown(object sender, KeyEventArgs args)
        {
            if (_editorMode)
            {
                var resetConfirmationOpen =
                    _activeEditorPage == SettingsPage.Layout &&
                    _layoutEditorView.HasPendingResetAll;
                if (args.Key == Key.Escape)
                {
                    if (_activeEditorPage == SettingsPage.Layout)
                    {
                        if (_layoutEditorView.CancelPendingResetAll())
                        {
                            args.Handled = true;
                            return;
                        }
                        _layoutEditorView.SelectEditorTab(SettingsPage.Features);
                    }
                    else
                    {
                        HandleSettingsIntent(new SettingsIntent(SettingsIntentKind.ReturnToGame));
                    }
                    args.Handled = true;
                    return;
                }

                if (resetConfirmationOpen && args.Key == Key.Tab)
                {
                    _layoutEditorView.FocusResetConfirmation(
                        Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));
                    args.Handled = true;
                    return;
                }

                if (resetConfirmationOpen && Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
                {
                    args.Handled = true;
                    return;
                }

                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
                {
                    if (args.SystemKey == Key.L)
                    {
                        _layoutEditorView.SelectEditorTab(SettingsPage.Layout);
                        args.Handled = true;
                        return;
                    }

                    if (args.SystemKey == Key.C)
                    {
                        _featureSettingsView.OpenFeature(HudWidgetRegistry.Upgrades);
                        _layoutEditorView.SelectEditorTab(SettingsPage.Features);
                        args.Handled = true;
                        return;
                    }

                    if (args.SystemKey == Key.F)
                    {
                        _layoutEditorView.SelectEditorTab(SettingsPage.Features);
                        args.Handled = true;
                        return;
                    }

                }
            }

        }
        private void OnClosing(object sender, CancelEventArgs args)
        {
            if (_shutdownRequested)
            {
                args.Cancel = !_resourcesDisposed;
                return;
            }

            args.Cancel = true;
            if (_editorMode)
            {
                HandleSettingsIntent(new SettingsIntent(SettingsIntentKind.CloseEditor));
            }
        }

        private void OnClosed(object sender, EventArgs args)
        {
            CompleteRuntimeTeardown();
        }

        private void CompleteRuntimeTeardown()
        {
            if (_resourcesDisposed)
            {
                return;
            }
            if (_coordinator != null && !_coordinator.IsShutdownComplete)
            {
                _shutdownBlocked = true;
                return;
            }

            DetachWindowSubscriptions();
            _resourcesDisposed = true;
            _applicationSession?.Dispose();
        }

    }
}
