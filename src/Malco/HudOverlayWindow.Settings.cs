using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Media;
using Malco.Configuration;
using Malco.Configuration.Models;
using Malco.Data;
using Malco.Localization;
using Malco.Models;
using Malco.Settings.Contracts;
using Malco.Settings.Controller;
using Malco.Settings.Views;
using Malco.Shell;

namespace Malco
{
    internal sealed partial class HudOverlayWindow
    {
        internal void RenderEditorTab()
        {
            if (_activeEditorPage == SettingsPage.Features)
            {
                _editorPanel.Visibility = Visibility.Collapsed;
                _featurePanel.Visibility = _editorMode ? Visibility.Visible : Visibility.Collapsed;
                _featureSettingsView.Refresh();
                return;
            }

            _editorPanel.Visibility = _editorMode ? Visibility.Visible : Visibility.Collapsed;
            _featurePanel.Visibility = Visibility.Collapsed;
            _layoutEditorView.RefreshLayoutEditorState();
        }

        private void OnSettingsStatusClock(object sender, EventArgs args)
        {
            if (_resourcesDisposed || _pendingSettingsRevision <= 0L)
            {
                _settingsStatusClock.Stop();
                return;
            }

            var latest = _settingsPersistence.LastFlushResult;
            if (!latest.HasValue)
            {
                return;
            }
            if (!latest.Value.Succeeded)
            {
                if (latest.Value.AttemptedRevision < _pendingSettingsRevision)
                {
                    return;
                }
                if (!SameFlushResult(_lastPresentedSettingsFlush, latest.Value))
                {
                    PresentSettingsFlushResult(latest.Value);
                }
                if (!_settingsPersistence.HasPendingAutosave)
                {
                    _pendingSettingsRevision = 0L;
                    _settingsStatusClock.Stop();
                }
                return;
            }
            if (latest.Value.FlushedRevision < _pendingSettingsRevision)
            {
                return;
            }

            PresentSettingsFlushResult(latest.Value);
            _pendingSettingsRevision = 0L;
            _settingsStatusClock.Stop();
        }

        private static bool SameFlushResult(SettingsFlushResult? left, SettingsFlushResult right)
            => left.HasValue &&
               left.Value.Status == right.Status &&
               left.Value.Reason == right.Reason &&
               left.Value.FlushedRevision == right.FlushedRevision &&
               left.Value.AttemptedRevision == right.AttemptedRevision &&
               string.Equals(left.Value.Message, right.Message, StringComparison.Ordinal);

        private void UpdateChannelDemand(FrozenSemanticSnapshot snapshot, bool rally, bool commands)
            => _sceneViewController.UpdateChannelDemand(snapshot, rally, commands);

        private void UpdatePresentationClockArming()
            => _sceneViewController.UpdatePresentationClockArming();

        internal OverlayRuntimeMode ResolveRuntimeMode(FrozenSemanticSnapshot snapshot)
        {
            if (_editorMode)
            {
                return OverlayRuntimeMode.Editor;
            }

            if (IsGameplaySnapshotReady(snapshot))
            {
                return OverlayRuntimeMode.Gameplay;
            }

            return OverlayRuntimeMode.SettingsOnly;
        }

        private static bool IsGameplaySnapshotReady(FrozenSemanticSnapshot snapshot)
        {
            return snapshot != null &&
                   snapshot.IsInMatch &&
                   snapshot.LocalPlayerId >= 0 &&
                   snapshot.Race != Race.Unknown;
        }

        internal void UpdateSettingsButtonStatus(string providerMessage, FrozenSemanticSnapshot snapshot)
        {
            var status = !string.IsNullOrWhiteSpace(providerMessage)
                ? providerMessage
                : snapshot != null ? snapshot.WorkerStateStatus : string.Empty;
            _settingsButton.ToolTip = string.IsNullOrWhiteSpace(status)
                ? UiText.Get("Settings")
                : status;
        }

        private void SetLayoutSaveStatus(string layoutText, string automationState, Brush foreground)
        {
            _layoutEditorView.SetSaveStatus(layoutText, automationState, foreground);
            _featureSettingsView.SetSaveStatus(layoutText, automationState, foreground);
        }

        private void SetLayoutSaveError(string message, bool visible)
        {
            _layoutEditorView.SetSaveError(message, visible);
            _featureSettingsView.SetSaveError(message, visible);
        }

        private bool SaveLayoutIfNeeded()
        {
            var result = _settingsPersistence.TryFlush(SettingsFlushReason.EditorExit);
            PresentSettingsFlushResult(result);
            if (result.Succeeded)
            {
                return true;
            }

            if (_activeEditorPage == SettingsPage.Layout)
            {
                _layoutEditorView.FocusSaveRecovery();
            }
            else
            {
                _featureSettingsView.FocusSaveRecovery();
            }
            return false;
        }

        private void RetrySettingsSave()
        {
            var result = _settingsPersistence.TryFlush(SettingsFlushReason.EditorExit);
            PresentSettingsFlushResult(result);
            if (!result.Succeeded)
            {
                if (_activeEditorPage == SettingsPage.Layout)
                {
                    _layoutEditorView.FocusSaveRecovery();
                }
                else
                {
                    _featureSettingsView.FocusSaveRecovery();
                }
            }
        }

        internal SettingsEditResult ApplySettingsEdit(SettingsEdit edit)
        {
            var result = _settingsPersistence.ApplyEdit(edit);
            if (result.Changed)
            {
                if (edit.Kind == SettingsEditKind.SetLanguage)
                {
                    RefreshLocalizedUi(_settingsController.Layout.Language);
                }
                _hudDisplayPreferences = HudDisplayPreferences.FromLayout(
                    _settingsController.Capture().Snapshot.ToMutable());
                if (edit.Kind == SettingsEditKind.SetIconSize)
                {
                    RefreshLayoutSamples();
                }
                _pendingSettingsRevision = result.Revision;
                _settingsStatusClock.Stop();
                _settingsStatusClock.Start();
                SetLayoutSaveStatus(UiText.Get("Saving..."), UiText.Get("Saving..."), SettingsAccentBrush);
            }

            return result;
        }

        private void RefreshLocalizedUi(string language)
        {
            var previousSettingsLabel = UiText.Get("Settings");
            UiText.Initialize(language);

            Title = UiText.Get("Malco");
            AutomationProperties.SetName(this, UiText.Get("Malco settings"));
            AutomationProperties.SetName(_root, UiText.Get("Malco settings"));
            _settingsButton.Content = UiText.Get("Settings");
            if (_settingsButton.ToolTip == null ||
                string.Equals(_settingsButton.ToolTip as string, previousSettingsLabel, StringComparison.Ordinal))
            {
                _settingsButton.ToolTip = UiText.Get("Settings");
            }

            _featureSettingsView.RefreshLanguage();
            _layoutEditorView.RefreshLanguage();
            foreach (var definition in HudWidgetRegistry.EditorFeatures())
            {
                HudWidgetView widget;
                if (!_widgets.TryGetValue(definition.Key, out widget) || widget == null)
                {
                    continue;
                }
                widget.SetTitle(UiText.Get(definition.Title));
                widget.SetSampleBody(BuildLayoutSample(definition.Key));
            }
            _trayController?.RefreshLanguage();
            ApplyWidgetEditorChrome();
            RefreshPresenterViews();

            if (_lastPresentedSettingsFlush.HasValue && !_lastPresentedSettingsFlush.Value.Succeeded)
            {
                SetLayoutSaveError(GetSettingsFlushUserMessage(_lastPresentedSettingsFlush.Value), true);
            }
            else if (_layoutLoadResult != null && _layoutLoadResult.IsWriteBlocked)
            {
                SetLayoutSaveError(
                    UiText.Get("Back up, move or rename hud-layout.json, then retry save."),
                    true);
            }
        }

        private void RefreshPresenterViews()
        {
            SetWidgetGameplayContent(
                HudWidgetRegistry.Workers,
                _workersPresenter.ApplyWorkers(
                    _sceneViewController.LatestSnapshot,
                    _scenePresenter.SessionGeneration,
                    _hudDisplayPreferences));
            _unitsPresenter.Invalidate();
            _buildingsPresenter.Invalidate();
            _upgradesPresenter.InvalidateVisuals();
            ApplyUnitAndBuildingPresenters(_sceneViewController.LatestSnapshot, _scenePresenter.SessionGeneration);
            ApplyUpgradePresenters(_sceneViewController.LatestSnapshot, _scenePresenter.SessionGeneration);
            ApplyAvailableUpgradePresenter();
            ApplyCompletedUpgradePresenters();
        }

        private void ApplyInitialLayoutLoadStatus()
        {
            if (_layoutLoadResult.IsWriteBlocked)
            {
                var recoveryMessage = UiText.Get(
                    "Back up, move or rename hud-layout.json, then retry save.");
                SetLayoutSaveStatus(
                    UiText.Get("Recovery required"),
                    UiText.Get("Recovery required; writes blocked"),
                    SettingsDangerBrush);
                _editorStatus.Text = recoveryMessage;
                AutomationProperties.SetHelpText(_layoutSaveStatus, recoveryMessage);
                SetLayoutSaveError(recoveryMessage, true);
            }
            else
            {
                SetLayoutSaveStatus(UiText.Get("Saved"), UiText.Get("Saved"), SettingsMutedBrush);
                SetLayoutSaveError(string.Empty, false);
            }
        }

        private void PresentSettingsFlushResult(SettingsFlushResult result)
        {
            _lastPresentedSettingsFlush = result;
            if (result.Succeeded)
            {
                if (_layoutLoadResult != null && _layoutLoadResult.IsWriteBlocked)
                {
                    _layoutLoadResult = new LayoutLoadResult(
                        LayoutLoadStatus.Loaded,
                        _settingsController.Layout,
                        false,
                        string.Empty);
                }
                SetLayoutSaveStatus(UiText.Get("Saved"), UiText.Get("Saved"), SettingsMutedBrush);
                SetLayoutSaveError(string.Empty, false);
                _editorStatus.Text = _lastShellStatusText ?? string.Empty;
                AutomationProperties.SetHelpText(_layoutSaveStatus, string.Empty);
            }
            else
            {
                var userMessage = GetSettingsFlushUserMessage(result);
                SetLayoutSaveStatus(
                    UiText.Get("Save failed - settings remain open"),
                    UiText.Get("Save failed - settings remain open"),
                    SettingsDangerBrush);
                _editorStatus.Text = userMessage;
                AutomationProperties.SetHelpText(_layoutSaveStatus, userMessage);
                SetLayoutSaveError(userMessage, true);
            }
        }

        private static string GetSettingsFlushUserMessage(SettingsFlushResult result)
        {
            return result.Status == SettingsFlushStatus.RecoveryRequired
                ? UiText.Get("Back up, move or rename hud-layout.json, then retry save.")
                : UiText.Get("Check the settings location, permissions, and available disk space, then retry.");
        }

    }
}
