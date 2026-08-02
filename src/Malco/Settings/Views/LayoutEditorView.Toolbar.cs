using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Threading;
using Malco.Localization;
using Malco.Settings.Contracts;

namespace Malco.Settings.Views
{
    internal sealed partial class LayoutEditorView
    {
        internal Border BuildEditorPanel()
        {
            _toolbarTitle = _palette.Text(UiText.Get("Arrange HUD"), 17d, FontWeights.Bold, _palette.TextBrush);
            _toolbarTitle.VerticalAlignment = VerticalAlignment.Center;
            Status.VerticalAlignment = VerticalAlignment.Center;
            Status.TextWrapping = TextWrapping.Wrap;
            Status.TextTrimming = TextTrimming.None;
            Status.MaxHeight = 34d;

            _resetSelectedButton = _chrome.ActionButton(UiText.Get("Reset panel"));
            _resetSelectedButton.Height = 44d;
            _resetSelectedButton.Margin = new Thickness(0d, 0d, 6d, 0d);
            _resetSelectedButton.Click += (sender, args) =>
            {
                _actions.ResetWidgetLayout(_actions.SelectedWidgetKey);
                RefreshLayoutEditorState();
            };
            _resetAllButton = _chrome.ActionButton(UiText.Get("Reset all"));
            _resetAllButton.Height = 44d;
            _resetAllButton.Width = 120d;
            _resetAllButton.Margin = new Thickness(0d, 0d, 8d, 0d);
            _resetAllButton.Click += (sender, args) =>
            {
                ShowResetAllConfirmation();
            };
            AutomationProperties.SetLiveSetting(_resetAllButton, AutomationLiveSetting.Assertive);
            SaveStatus.VerticalAlignment = VerticalAlignment.Center;
            _doneButton = _chrome.ActionButton(UiText.Get("Done"));
            _doneButton.Height = 44d;
            _doneButton.MinWidth = 88d;
            _doneButton.Margin = new Thickness(0d);
            _doneButton.Background = _palette.AccentBrush;
            _doneButton.BorderBrush = _palette.AccentBrush;
            _doneButton.Foreground = _palette.InkBrush;
            _doneButton.Tag = "Primary";
            _doneButton.Click += (sender, args) => _actions.Dispatch(new SettingsIntent(SettingsIntentKind.OpenFeatures));
            _moveToolbarThumb = BuildToolbarMoveThumb();

            var cardContent = new Grid();
            cardContent.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            cardContent.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            cardContent.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _toolbarContainer = new Grid();
            cardContent.Children.Add(_toolbarContainer);
            _resetConfirmationBar = BuildResetConfirmationBar();
            Grid.SetRow(_resetConfirmationBar, 1);
            cardContent.Children.Add(_resetConfirmationBar);
            _saveErrorBanner = BuildSaveErrorBanner();
            Grid.SetRow(_saveErrorBanner, 2);
            cardContent.Children.Add(_saveErrorBanner);
            RebuildToolbar(_actions.ViewWidth > 0d && _actions.ViewWidth < 720d);

            _toolbarCard = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                MaxWidth = 720d,
                Margin = new Thickness(16d, 0d, 16d, 16d),
                Background = _palette.PanelBrush,
                BorderBrush = _palette.BorderBrush,
                BorderThickness = new Thickness(1d),
                CornerRadius = new CornerRadius(4d),
                Child = cardContent,
                RenderTransform = _toolbarOffset
            };
            _toolbarCard.SizeChanged += (sender, args) =>
                _actions.Dispatcher.BeginInvoke(
                    new Action(() => MoveToolbar(0d, 0d)),
                    DispatcherPriority.Loaded);
            System.Windows.Controls.Panel.SetZIndex(_toolbarCard, 100);
            return _toolbarCard;
        }

        private void RebuildToolbar(bool compact)
        {
            FrameworkElement focusedControl = null;
            var toolbarHadFocus = _currentToolbar != null && _currentToolbar.IsKeyboardFocusWithin;
            if (_moveToolbarThumb != null && _moveToolbarThumb.IsKeyboardFocusWithin)
            {
                focusedControl = _moveToolbarThumb;
            }
            else if (_resetSelectedButton != null && _resetSelectedButton.IsKeyboardFocusWithin)
            {
                focusedControl = _resetSelectedButton;
            }
            else if (_resetAllButton != null && _resetAllButton.IsKeyboardFocusWithin)
            {
                focusedControl = _resetAllButton;
            }
            else if (_doneButton != null && _doneButton.IsKeyboardFocusWithin)
            {
                focusedControl = _doneButton;
            }
            if (_currentToolbar != null)
            {
                _currentToolbar.Children.Clear();
            }
            DetachElement(_toolbarTitle);
            DetachElement(Status);
            DetachElement(_moveToolbarThumb);
            DetachElement(_resetSelectedButton);
            DetachElement(_resetAllButton);
            DetachElement(SaveStatus);
            DetachElement(_doneButton);
            _toolbarContainer.Children.Clear();
            _compactToolbar = compact;

            var toolbar = new Grid
            {
                Margin = compact ? new Thickness(12d, 4d, 12d, 8d) : new Thickness(16d, 0d, 16d, 0d),
                MinHeight = 56d
            };
            if (compact)
            {
                toolbar.RowDefinitions.Add(new RowDefinition { Height = new GridLength(44d) });
                toolbar.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var summary = new Grid();
                summary.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                summary.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                summary.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
                summary.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                _moveToolbarThumb.Margin = new Thickness(0d, 0d, 8d, 0d);
                summary.Children.Add(_moveToolbarThumb);
                _toolbarTitle.Margin = new Thickness(0d, 0d, 10d, 0d);
                Grid.SetColumn(_toolbarTitle, 1);
                summary.Children.Add(_toolbarTitle);
                Status.Margin = new Thickness(0d, 0d, 10d, 0d);
                Grid.SetColumn(Status, 2);
                summary.Children.Add(Status);
                SaveStatus.Margin = new Thickness(0d);
                Grid.SetColumn(SaveStatus, 3);
                summary.Children.Add(SaveStatus);
                toolbar.Children.Add(summary);

                var actions = new WrapPanel
                {
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0d, 4d, 0d, 0d)
                };
                _resetSelectedButton.Margin = new Thickness(0d, 0d, 6d, 0d);
                _resetAllButton.Margin = new Thickness(0d, 0d, 8d, 0d);
                actions.Children.Add(_resetSelectedButton);
                actions.Children.Add(_resetAllButton);
                actions.Children.Add(_doneButton);
                Grid.SetRow(actions, 1);
                toolbar.Children.Add(actions);
            }
            else
            {
                toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
                toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var identity = new Grid
                {
                    Width = 208d,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0d, 0d, 14d, 0d)
                };
                identity.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                identity.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
                _moveToolbarThumb.Margin = new Thickness(0d, 0d, 8d, 0d);
                identity.Children.Add(_moveToolbarThumb);
                var summary = new StackPanel { Width = 156d };
                _toolbarTitle.Margin = new Thickness(0d);
                summary.Children.Add(_toolbarTitle);
                Status.Margin = new Thickness(0d, 2d, 0d, 0d);
                Status.MaxWidth = 156d;
                summary.Children.Add(Status);
                Grid.SetColumn(summary, 1);
                identity.Children.Add(summary);
                toolbar.Children.Add(identity);
                _resetSelectedButton.Margin = new Thickness(0d, 0d, 6d, 0d);
                Grid.SetColumn(_resetSelectedButton, 1);
                toolbar.Children.Add(_resetSelectedButton);
                _resetAllButton.Margin = new Thickness(0d, 0d, 8d, 0d);
                Grid.SetColumn(_resetAllButton, 2);
                toolbar.Children.Add(_resetAllButton);
                SaveStatus.Margin = new Thickness(4d, 0d, 12d, 0d);
                SaveStatus.HorizontalAlignment = HorizontalAlignment.Center;
                Grid.SetColumn(SaveStatus, 3);
                toolbar.Children.Add(SaveStatus);
                Grid.SetColumn(_doneButton, 4);
                toolbar.Children.Add(_doneButton);
            }

            _currentToolbar = toolbar;
            _toolbarContainer.Children.Add(toolbar);
            RefreshLayoutEditorState();
            if (toolbarHadFocus)
            {
                _actions.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (focusedControl == null || !focusedControl.Focus())
                    {
                        FocusPrimaryControl();
                    }
                }), DispatcherPriority.Loaded);
            }
        }

        private Thumb BuildToolbarMoveThumb()
        {
            var xaml = @"
<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                  xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
                  TargetType='{x:Type Thumb}'>
  <Border x:Name='Frame' Background='$RAISED_SURFACE' BorderBrush='$CONTROL_BORDER'
          BorderThickness='1' CornerRadius='3'>
    <TextBlock Text='&#x2725;' Foreground='$TEXT_SECONDARY' FontSize='16'
               HorizontalAlignment='Center' VerticalAlignment='Center'/>
  </Border>
  <ControlTemplate.Triggers>
    <Trigger Property='IsMouseOver' Value='True'>
      <Setter TargetName='Frame' Property='Background' Value='$HOVER_SURFACE'/>
      <Setter TargetName='Frame' Property='BorderBrush' Value='$ACCENT_HOVER'/>
    </Trigger>
    <Trigger Property='IsKeyboardFocused' Value='True'>
      <Setter TargetName='Frame' Property='BorderBrush' Value='$FOCUS'/>
      <Setter TargetName='Frame' Property='BorderThickness' Value='2'/>
    </Trigger>
    <Trigger Property='IsDragging' Value='True'>
      <Setter TargetName='Frame' Property='BorderBrush' Value='$ACCENT'/>
      <Setter TargetName='Frame' Property='BorderThickness' Value='2'/>
    </Trigger>
  </ControlTemplate.Triggers>
</ControlTemplate>";
            xaml = xaml
                .Replace("$RAISED_SURFACE", SettingsVisualTokens.RaisedSurface)
                .Replace("$CONTROL_BORDER", SettingsVisualTokens.ControlBorder)
                .Replace("$TEXT_SECONDARY", SettingsVisualTokens.TextSecondary)
                .Replace("$HOVER_SURFACE", SettingsVisualTokens.HoverSurface)
                .Replace("$ACCENT_HOVER", SettingsVisualTokens.AccentHover)
                .Replace("$ACCENT", SettingsVisualTokens.Accent)
                .Replace("$FOCUS", SettingsVisualTokens.FocusRing);
            var thumb = new Thumb
            {
                Width = 44d,
                Height = 44d,
                Cursor = Cursors.SizeAll,
                Focusable = true,
                ToolTip = UiText.Get("Move toolbar"),
                Template = (ControlTemplate)XamlReader.Parse(xaml)
            };
            AutomationProperties.SetName(thumb, UiText.Get("Move toolbar"));
            AutomationProperties.SetHelpText(thumb, UiText.Get("Drag or use arrow keys to move the toolbar."));
            thumb.DragDelta += (sender, args) => MoveToolbar(args.HorizontalChange, args.VerticalChange);
            thumb.PreviewKeyDown += (sender, args) =>
            {
                const double step = 16d;
                switch (args.Key)
                {
                    case Key.Left:
                        MoveToolbar(-step, 0d);
                        break;
                    case Key.Right:
                        MoveToolbar(step, 0d);
                        break;
                    case Key.Up:
                        MoveToolbar(0d, -step);
                        break;
                    case Key.Down:
                        MoveToolbar(0d, step);
                        break;
                    default:
                        return;
                }
                args.Handled = true;
            };
            return thumb;
        }

        private void MoveToolbar(double horizontalChange, double verticalChange)
        {
            if (_toolbarCard == null)
            {
                return;
            }

            var host = _toolbarCard.Parent as FrameworkElement;
            if (host == null || host.ActualWidth <= 0d || host.ActualHeight <= 0d ||
                _toolbarCard.ActualWidth <= 0d || _toolbarCard.ActualHeight <= 0d)
            {
                return;
            }

            const double edgePadding = 8d;
            var initialLeft = (host.ActualWidth - _toolbarCard.ActualWidth) / 2d;
            var initialTop = host.ActualHeight - _toolbarCard.ActualHeight - _toolbarCard.Margin.Bottom;
            var minimumX = edgePadding - initialLeft;
            var maximumX = host.ActualWidth - _toolbarCard.ActualWidth - edgePadding - initialLeft;
            var minimumY = edgePadding - initialTop;
            var maximumY = host.ActualHeight - _toolbarCard.ActualHeight - edgePadding - initialTop;
            _toolbarOffset.X = Math.Max(minimumX, Math.Min(maximumX, _toolbarOffset.X + horizontalChange));
            _toolbarOffset.Y = Math.Max(minimumY, Math.Min(maximumY, _toolbarOffset.Y + verticalChange));
        }

        private void DetachElement(FrameworkElement element)
        {
            if (element == null)
            {
                return;
            }
            var panel = element.Parent as Panel;
            if (panel != null)
            {
                panel.Children.Remove(element);
                return;
            }
            var contentControl = element.Parent as ContentControl;
            if (contentControl != null && ReferenceEquals(contentControl.Content, element))
            {
                contentControl.Content = null;
                return;
            }
            var decorator = element.Parent as Decorator;
            if (decorator != null && ReferenceEquals(decorator.Child, element))
            {
                decorator.Child = null;
            }
        }
    }
}
