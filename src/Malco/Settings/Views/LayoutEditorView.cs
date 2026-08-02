using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using Malco.Localization;

namespace Malco.Settings.Views
{
    internal sealed partial class LayoutEditorView
    {
        private readonly ISettingsViewActions _actions;
        private readonly ISettingsEditorChrome _chrome;
        private readonly SettingsViewPalette _palette;
        private Button _resetSelectedButton;
        private Button _resetAllButton;
        private Border _resetConfirmationBar;
        private TextBlock _resetConfirmationMessage;
        private Button _confirmResetAllButton;
        private Button _cancelResetAllButton;
        private Border _saveErrorBanner;
        private TextBlock _saveErrorMessage;
        private Button _retrySaveButton;
        private Grid _toolbarContainer;
        private Grid _currentToolbar;
        private Border _toolbarCard;
        private TextBlock _toolbarTitle;
        private Thumb _moveToolbarThumb;
        private Button _doneButton;
        private readonly TranslateTransform _toolbarOffset = new TranslateTransform();
        private bool _confirmingResetAll;
        private bool _compactToolbar;

        public LayoutEditorView(
            ISettingsViewActions actions,
            ISettingsEditorChrome chrome,
            SettingsViewPalette palette,
            Brush savedBrush)
        {
            _actions = actions ?? throw new ArgumentNullException(nameof(actions));
            _chrome = chrome ?? throw new ArgumentNullException(nameof(chrome));
            _palette = palette ?? throw new ArgumentNullException(nameof(palette));
            Status = _palette.Text(string.Empty, 12d, FontWeights.SemiBold, _palette.TextBrush);
            SaveStatus = _palette.Text(UiText.Get("Saved"), 11d, FontWeights.SemiBold, savedBrush);
            SaveStatus.MaxWidth = 112d;
            SaveStatus.TextWrapping = TextWrapping.NoWrap;
            SaveStatus.TextTrimming = TextTrimming.CharacterEllipsis;
            SaveStatus.ToolTip = UiText.Get("Saved");
            AutomationProperties.SetName(SaveStatus, UiText.Get("Layout") + ": " + UiText.Get("Saved"));
            AutomationProperties.SetLiveSetting(SaveStatus, AutomationLiveSetting.Polite);
        }

        public TextBlock SaveStatus { get; }
        public TextBlock Status { get; }

        public void SetSaveStatus(string text, string automationState, Brush foreground)
        {
            SaveStatus.Text = text ?? string.Empty;
            SaveStatus.Foreground = foreground;
            SaveStatus.ToolTip = text ?? string.Empty;
            AutomationProperties.SetName(SaveStatus, UiText.Get("Layout") + ": " + (automationState ?? string.Empty));
        }

        public void RefreshLanguage()
        {
            SetButtonText(_resetSelectedButton, "Reset panel");
            SetButtonText(_resetAllButton, "Reset all");
            SetButtonText(_doneButton, "Done");
            SetButtonText(_retrySaveButton, "Retry save");
            SetButtonText(_confirmResetAllButton, "Confirm reset");
            SetButtonText(_cancelResetAllButton, "Cancel");
            if (_toolbarTitle != null)
            {
                _toolbarTitle.Text = UiText.Get("Arrange HUD");
            }
            if (_moveToolbarThumb != null)
            {
                _moveToolbarThumb.ToolTip = UiText.Get("Move toolbar");
                AutomationProperties.SetName(_moveToolbarThumb, UiText.Get("Move toolbar"));
                AutomationProperties.SetHelpText(_moveToolbarThumb, UiText.Get("Drag or use arrow keys to move the toolbar."));
            }
            if (_resetConfirmationMessage != null)
            {
                _resetConfirmationMessage.Text = UiText.Get("Reset every HUD panel to its default position and size?");
            }
            if (_resetConfirmationBar != null)
            {
                AutomationProperties.SetName(
                    _resetConfirmationBar,
                    UiText.Get("Reset every HUD panel to its default position and size?"));
            }
            if (_resetAllButton != null)
            {
                AutomationProperties.SetItemStatus(
                    _resetAllButton,
                    _confirmingResetAll ? UiText.Get("Confirm reset") : string.Empty);
            }
            RefreshLayoutEditorState();
        }

        public void SetSaveError(string message, bool visible)
        {
            if (_saveErrorBanner == null || _saveErrorMessage == null)
            {
                return;
            }

            _saveErrorMessage.Text = visible
                ? (message ?? UiText.Get("Settings could not be saved."))
                : string.Empty;
            _saveErrorBanner.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            AutomationProperties.SetName(_saveErrorBanner, _saveErrorMessage.Text);
        }

        public void FocusSaveRecovery()
        {
            _retrySaveButton?.Focus();
        }

        public bool CancelPendingResetAll()
        {
            if (!_confirmingResetAll)
            {
                return false;
            }
            ClearResetAllConfirmation();
            _resetAllButton?.Focus();
            return true;
        }

        public bool HasPendingResetAll => _confirmingResetAll;

        public bool IsResetConfirmationInteraction(DependencyObject source)
            => _confirmingResetAll &&
               source != null &&
               _resetConfirmationBar != null &&
               (ReferenceEquals(source, _resetConfirmationBar) || _resetConfirmationBar.IsAncestorOf(source));

        public void FocusResetConfirmation(bool reverse = false)
        {
            if (!_confirmingResetAll)
            {
                return;
            }
            var target = reverse
                ? _cancelResetAllButton != null && _cancelResetAllButton.IsKeyboardFocusWithin
                    ? _confirmResetAllButton
                    : _cancelResetAllButton
                : _confirmResetAllButton != null && _confirmResetAllButton.IsKeyboardFocusWithin
                    ? _cancelResetAllButton
                    : _confirmResetAllButton;
            target?.Focus();
        }

        public bool FocusPrimaryControl()
        {
            if (_confirmingResetAll)
            {
                FocusResetConfirmation();
                return true;
            }
            if (_resetSelectedButton != null && _resetSelectedButton.IsEnabled && _resetSelectedButton.Focus())
            {
                return true;
            }
            return _doneButton != null && _doneButton.Focus();
        }

        public void UpdateResponsiveLayout(double width)
        {
            var compact = width > 0d && width < 720d;
            if (_toolbarContainer == null)
            {
                return;
            }

            if (compact != _compactToolbar)
            {
                RebuildToolbar(compact);
            }
            _actions.Dispatcher.BeginInvoke(
                new Action(() => MoveToolbar(0d, 0d)),
                DispatcherPriority.Loaded);
        }
    }
}
