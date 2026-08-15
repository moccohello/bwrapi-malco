using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Malco.Configuration.Models;
using Malco.Localization;
using Malco.Settings.Contracts;

namespace Malco.Settings.Views
{
    internal sealed partial class FeatureSettingsView
    {
        private FrameworkElement BuildGeneralDetail()
        {
            var content = new StackPanel
            {
                MaxWidth = 760d,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            content.Children.Add(_palette.Text(UiText.Get("General"), 20d, FontWeights.Bold, _palette.TextBrush));
            var subtitle = _palette.Text(UiText.Get("Language and app-wide preferences."), 13d, FontWeights.Normal, _palette.MutedBrush);
            subtitle.Margin = new Thickness(0d, 4d, 0d, 24d);
            content.Children.Add(subtitle);
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var copy = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            copy.Children.Add(_palette.Text(UiText.Get("Language"), 14d, FontWeights.SemiBold, _palette.TextBrush));
            row.Children.Add(copy);
            var current = MalcoPreferenceValues.NormalizeLanguage(_actions.LayoutSnapshot.Language);
            var language = new StackPanel { Orientation = Orientation.Horizontal };
            var english = SegmentButton("English", string.Equals(current, MalcoPreferenceValues.English, StringComparison.Ordinal), "settings-language");
            english.Click += (sender, args) =>
            {
                SetLanguage(MalcoPreferenceValues.English);
                RestoreFocusAfterRender("English");
            };
            language.Children.Add(english);
            var korean = SegmentButton("한국어", string.Equals(current, MalcoPreferenceValues.Korean, StringComparison.Ordinal), "settings-language");
            korean.Click += (sender, args) =>
            {
                SetLanguage(MalcoPreferenceValues.Korean);
                RestoreFocusAfterRender("한국어");
            };
            language.Children.Add(korean);
            if (_compactLayout)
            {
                var stack = new StackPanel();
                row.Children.Clear();
                stack.Children.Add(copy);
                language.Margin = new Thickness(0d, 12d, 0d, 0d);
                stack.Children.Add(language);
                row.Children.Add(stack);
            }
            else
            {
                Grid.SetColumn(language, 1);
                row.Children.Add(language);
            }
            content.Children.Add(new Border
            {
                Padding = new Thickness(16d),
                Background = _palette.SurfaceBrush,
                BorderBrush = _palette.SeparatorBrush,
                BorderThickness = new Thickness(0d, 1d, 0d, 1d),
                Child = row
            });
            var versionRow = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            versionRow.Children.Add(_palette.Text(UiText.Get("Program version"), 14d, FontWeights.SemiBold, _palette.TextBrush));
            var versionText = _palette.Text(
                _actions.ProgramVersion,
                12d,
                FontWeights.Normal,
                _palette.MutedBrush);
            versionText.Margin = new Thickness(0d, 4d, 0d, 0d);
            versionRow.Children.Add(versionText);
            content.Children.Add(new Border
            {
                Padding = new Thickness(16d),
                Background = _palette.SurfaceBrush,
                BorderBrush = _palette.SeparatorBrush,
                BorderThickness = new Thickness(0d, 0d, 0d, 1d),
                Child = versionRow
            });
            return content;
        }

        private ToggleButton BuildSwitch(string automationName, bool enabled, Action<bool> setter)
        {
            var toggle = new ToggleButton
            {
                Width = 52d,
                Height = 44d,
                IsChecked = enabled,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0d),
                Style = FeatureSettingsStyles.CreateSwitch()
            };
            AutomationProperties.SetName(toggle, automationName);
            var status = UiText.Get(enabled ? "On" : "Off");
            AutomationProperties.SetItemStatus(toggle, status);
            AutomationProperties.SetHelpText(toggle, status);
            toggle.Click += (sender, args) =>
            {
                var next = toggle.IsChecked == true;
                var nextStatus = UiText.Get(next ? "On" : "Off");
                AutomationProperties.SetItemStatus(toggle, nextStatus);
                AutomationProperties.SetHelpText(toggle, nextStatus);
                setter(next);
            };
            return toggle;
        }

        private RadioButton SegmentButton(string label, bool selected, string groupName)
        {
            var button = new RadioButton
            {
                Content = label,
                GroupName = groupName,
                IsChecked = selected,
                Height = 44d,
                MinWidth = 96d,
                Margin = new Thickness(0d, 0d, -1d, 0d),
                Padding = new Thickness(14d, 0d, 14d, 0d),
                Foreground = _palette.TextBrush,
                Background = _palette.SurfaceBrush,
                BorderBrush = _palette.BorderBrush,
                BorderThickness = new Thickness(1d),
                FontSize = 12d,
                FontWeight = FontWeights.SemiBold,
                Style = FeatureSettingsStyles.CreateSegment()
            };
            AutomationProperties.SetName(button, label);
            var status = UiText.Get(selected ? "Selected" : "Not selected");
            AutomationProperties.SetItemStatus(button, status);
            AutomationProperties.SetHelpText(button, status);
            return button;
        }

        private void RestoreFocusAfterRender(string automationName, bool fallbackToSelectedNavigation = false)
        {
            if (string.IsNullOrWhiteSpace(automationName) && !fallbackToSelectedNavigation)
            {
                return;
            }

            var target = string.IsNullOrWhiteSpace(automationName)
                ? null
                : FindAutomationTarget(_navigationHost, automationName) ??
                  FindAutomationTarget(_detailHost, automationName);
            if (target == null && fallbackToSelectedNavigation)
            {
                target = FocusButton;
            }
            FocusWhenLoaded(target);
        }

        private void FocusWhenLoaded(FrameworkElement target)
        {
            if (target == null)
            {
                return;
            }
            if (!target.IsLoaded)
            {
                if (ReferenceEquals(_pendingFocusTarget, target))
                {
                    return;
                }

                ClearPendingFocus();
                _pendingFocusTarget = target;
                _pendingFocusLoadedHandler = (sender, args) =>
                {
                    var pending = _pendingFocusTarget;
                    ClearPendingFocus();
                    FocusWhenLoaded(pending);
                };
                target.Loaded += _pendingFocusLoadedHandler;
                return;
            }

            ClearPendingFocus();
            target.BringIntoView();
            FocusManager.SetFocusedElement(FocusManager.GetFocusScope(target), target);
            target.Focus();
            Keyboard.Focus(target);
        }

        private void ClearPendingFocus()
        {
            if (_pendingFocusTarget != null && _pendingFocusLoadedHandler != null)
            {
                _pendingFocusTarget.Loaded -= _pendingFocusLoadedHandler;
            }
            _pendingFocusTarget = null;
            _pendingFocusLoadedHandler = null;
        }

        private static FrameworkElement FindAutomationTarget(DependencyObject root, string automationName)
        {
            var element = root as FrameworkElement;
            if (element != null &&
                string.Equals(AutomationProperties.GetName(element), automationName, StringComparison.Ordinal))
            {
                return element;
            }

            foreach (var child in LogicalTreeHelper.GetChildren(root))
            {
                var dependencyChild = child as DependencyObject;
                if (dependencyChild == null)
                {
                    continue;
                }

                var match = FindAutomationTarget(dependencyChild, automationName);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private FrameworkElement BuildEmptyDetail(string text)
        {
            var message = _palette.Text(text, 13d, FontWeights.Normal, _palette.MutedBrush);
            message.TextAlignment = TextAlignment.Center;
            message.HorizontalAlignment = HorizontalAlignment.Center;
            message.VerticalAlignment = VerticalAlignment.Center;
            AutomationProperties.SetName(message, text);
            AutomationProperties.SetLiveSetting(message, AutomationLiveSetting.Polite);
            return new Border
            {
                MinHeight = 120d,
                Margin = new Thickness(0d, 20d, 0d, 0d),
                Background = _palette.SurfaceBrush,
                BorderBrush = _palette.SeparatorBrush,
                BorderThickness = new Thickness(0d, 1d, 0d, 1d),
                Child = message
            };
        }

        private void OpenLayoutForSelectedFeature()
        {
            if (!HudFeatureCatalog.IsGeneral(_selectedFeatureKey) &&
                !HudFeatureCatalog.IsSpatial(_selectedFeatureKey))
            {
                OpenLayout(_selectedFeatureKey);
                return;
            }
            OpenLayout(string.Empty);
        }

        private void OpenLayout(string key)
        {
            _actions.SelectWidget(key);
            _actions.ActiveEditorPage = SettingsPage.Layout;
            _actions.UpdateEditorPlacement();
            _actions.RefreshEditorView();
            _actions.RefreshVisibility();
            _actions.FocusActiveEditorSurface();
        }

        private void SetCompletionMode(string mode)
        {
            _actions.ApplyEdit(SettingsEdit.SetCompletionDisplayMode(mode));
            _actions.RefreshPresenterViews();
            RenderDetail();
        }

        private void SetCompletionCountdownSeconds(int seconds)
        {
            _actions.ApplyEdit(SettingsEdit.SetCompletionCountdownSeconds(seconds));
            _actions.RefreshPresenterViews();
            RenderDetail();
        }

        private void SetLanguage(string language)
        {
            _actions.ApplyEdit(SettingsEdit.SetLanguage(language));
        }

        private static bool IsSearchMatch(TechTreeItem item, string searchText)
        {
            if (string.IsNullOrWhiteSpace(searchText))
            {
                return true;
            }
            return UiText.GameName(item.Name).IndexOf(searchText.Trim(), StringComparison.CurrentCultureIgnoreCase) >= 0;
        }

    }
}
