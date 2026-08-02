using System;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Malco.Localization;
using Malco.Settings.Contracts;

namespace Malco.Settings.Views
{
    internal sealed partial class LayoutEditorView
    {
        private Border BuildSaveErrorBanner()
        {
            var content = new Grid { Margin = new Thickness(12d, 8d, 12d, 8d) };
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24d) });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            content.Children.Add(_palette.Text("!", 16d, FontWeights.Bold, _palette.DangerBrush));
            _saveErrorMessage = _palette.Text(string.Empty, 12d, FontWeights.SemiBold, _palette.TextBrush);
            _saveErrorMessage.TextWrapping = TextWrapping.Wrap;
            _saveErrorMessage.TextTrimming = TextTrimming.None;
            AutomationProperties.SetLiveSetting(_saveErrorMessage, AutomationLiveSetting.Assertive);
            Grid.SetColumn(_saveErrorMessage, 1);
            content.Children.Add(_saveErrorMessage);
            _retrySaveButton = _chrome.ActionButton(UiText.Get("Retry save"));
            _retrySaveButton.Height = 44d;
            _retrySaveButton.Click += (sender, args) => _actions.RetrySettingsSave();
            Grid.SetColumn(_retrySaveButton, 2);
            content.Children.Add(_retrySaveButton);
            return new Border
            {
                Visibility = Visibility.Collapsed,
                Background = _palette.DangerSurfaceBrush,
                BorderBrush = _palette.DangerBrush,
                BorderThickness = new Thickness(3d, 0d, 0d, 0d),
                Child = content
            };
        }

        private Border BuildResetConfirmationBar()
        {
            var content = new Grid { Margin = new Thickness(12d, 8d, 12d, 8d) };
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _resetConfirmationMessage = _palette.Text(
                UiText.Get("Reset every HUD panel to its default position and size?"),
                12d,
                FontWeights.SemiBold,
                _palette.TextBrush);
            _resetConfirmationMessage.TextWrapping = TextWrapping.Wrap;
            _resetConfirmationMessage.VerticalAlignment = VerticalAlignment.Center;
            content.Children.Add(_resetConfirmationMessage);

            _confirmResetAllButton = _chrome.ActionButton(UiText.Get("Confirm reset"));
            _confirmResetAllButton.Background = _palette.WarningBrush;
            _confirmResetAllButton.BorderBrush = _palette.WarningBrush;
            _confirmResetAllButton.Foreground = _palette.InkBrush;
            _confirmResetAllButton.Tag = "Warning";
            _confirmResetAllButton.Click += (sender, args) =>
            {
                _actions.ResetAllWidgetLayouts();
                ClearResetAllConfirmation();
                _resetAllButton?.Focus();
            };
            Grid.SetColumn(_confirmResetAllButton, 1);
            content.Children.Add(_confirmResetAllButton);

            _cancelResetAllButton = _chrome.ActionButton(UiText.Get("Cancel"));
            _cancelResetAllButton.Click += (sender, args) =>
            {
                ClearResetAllConfirmation();
                _resetAllButton?.Focus();
            };
            Grid.SetColumn(_cancelResetAllButton, 2);
            content.Children.Add(_cancelResetAllButton);

            var bar = new Border
            {
                Visibility = Visibility.Collapsed,
                Background = _palette.WarningSurfaceBrush,
                BorderBrush = _palette.WarningBrush,
                BorderThickness = new Thickness(3d, 0d, 0d, 0d),
                Child = content
            };
            AutomationProperties.SetName(bar,
                UiText.Get("Reset every HUD panel to its default position and size?"));
            AutomationProperties.SetLiveSetting(bar, AutomationLiveSetting.Assertive);
            return bar;
        }

        internal void SelectEditorTab(SettingsPage page)
        {
            _actions.ActiveEditorPage = page;
            if (page == SettingsPage.Layout)
            {
                _toolbarOffset.X = 0d;
                _toolbarOffset.Y = 0d;
            }
            _actions.UpdateEditorPlacement();
            _actions.RefreshEditorView();
            _actions.RefreshVisibility();
            _actions.FocusActiveEditorSurface();
        }

        internal void OnCanvasWidgetSelected(object sender, EventArgs args)
        {
            var widget = sender as HudWidgetView;
            if (widget == null)
            {
                return;
            }
            _actions.SelectedWidgetKey = widget.Key;
            _actions.SelectWidget(widget.Key);
            RefreshLayoutEditorState();
        }

        internal void RefreshLayoutEditorState()
        {
            var definition = HudWidgetRegistry.EditorFeatures().FirstOrDefault(feature =>
                string.Equals(feature.Key, _actions.SelectedWidgetKey, StringComparison.OrdinalIgnoreCase));
            WidgetLayout selectedLayout = null;
            if (definition != null && _actions.Layout.Widgets != null)
            {
                _actions.Layout.Widgets.TryGetValue(definition.Key, out selectedLayout);
            }
            var hidden = selectedLayout != null && !selectedLayout.Enabled;
            Status.Text = definition == null
                ? UiText.Get("Select a HUD panel")
                : UiText.Get(definition.EditorLabel) +
                  (hidden ? " | " + UiText.Get("Hidden in game") : string.Empty);
            Status.ToolTip = definition == null ? null : UiText.Get(definition.EditorLabel);
            if (_resetSelectedButton != null)
            {
                _resetSelectedButton.IsEnabled = definition != null &&
                    !_confirmingResetAll &&
                    !HudWidgetRegistry.IsSpatialFeature(definition.Key);
            }
        }

        private void ClearResetAllConfirmation()
        {
            _confirmingResetAll = false;
            if (_resetAllButton == null)
            {
                return;
            }
            _resetAllButton.Content = UiText.Get("Reset all");
            _resetAllButton.IsEnabled = true;
            _resetAllButton.BorderBrush = _palette.BorderBrush;
            AutomationProperties.SetName(_resetAllButton, UiText.Get("Reset all"));
            AutomationProperties.SetItemStatus(_resetAllButton, string.Empty);
            if (_resetConfirmationBar != null)
            {
                _resetConfirmationBar.Visibility = Visibility.Collapsed;
            }
            if (_moveToolbarThumb != null)
            {
                _moveToolbarThumb.IsEnabled = true;
            }
            if (_doneButton != null)
            {
                _doneButton.IsEnabled = true;
            }
            RefreshLayoutEditorState();
        }

        private void ShowResetAllConfirmation()
        {
            if (_confirmingResetAll || _resetConfirmationBar == null)
            {
                return;
            }

            _confirmingResetAll = true;
            _resetAllButton.IsEnabled = false;
            if (_resetSelectedButton != null)
            {
                _resetSelectedButton.IsEnabled = false;
            }
            if (_moveToolbarThumb != null)
            {
                _moveToolbarThumb.IsEnabled = false;
            }
            if (_doneButton != null)
            {
                _doneButton.IsEnabled = false;
            }
            AutomationProperties.SetItemStatus(_resetAllButton, UiText.Get("Confirm reset"));
            _resetConfirmationBar.Visibility = Visibility.Visible;
            _confirmResetAllButton?.Focus();
        }

        private static void SetButtonText(Button button, string key)
        {
            if (button == null)
            {
                return;
            }
            var text = UiText.Get(key);
            button.Content = text;
            AutomationProperties.SetName(button, text);
        }
    }
}
