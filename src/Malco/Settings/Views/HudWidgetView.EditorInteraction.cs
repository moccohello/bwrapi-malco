using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Malco.Localization;

namespace Malco.Settings.Views
{
    internal sealed partial class HudWidgetView
    {
        public void SetEditorMode(bool enabled, bool hidden, bool selected)
        {
            _editorMode = enabled;
            Handle.ContentGrid.ClipToBounds = true;
            var editable = enabled;
            Root.BorderBrush = !enabled
                ? Brushes.Transparent
                : selected
                    ? EditorAccentBrush
                    : EditorBorderBrush;
            Root.BorderThickness = !enabled
                ? new Thickness(0d)
                : selected ? new Thickness(2d) : new Thickness(1d);
            Root.Background = enabled
                ? selected ? EditorSelectedSurfaceBrush : EditorSurfaceBrush
                : Brushes.Transparent;
            Root.Opacity = enabled && hidden ? 0.58d : 1d;
            RefreshEditorChromeVisibility();
            var editorTitle = Handle.EditorChrome.Child as TextBlock;
            if (editorTitle != null)
            {
                editorTitle.Text = hidden
                    ? Title + " | " + UiText.Get("Hidden in game")
                    : Title;
            }
            Root.ToolTip = hidden ? Title + " | " + UiText.Get("Hidden in game") : Title;
            Handle.SampleHost.Padding = new Thickness(0d);
            Handle.MoveThumb.Visibility = editable ? Visibility.Visible : Visibility.Collapsed;
            Handle.MoveThumb.Focusable = editable;
            KeyboardNavigation.SetIsTabStop(Handle.MoveThumb, editable);
            Handle.ResizeThumb.Visibility = editable ? Visibility.Visible : Visibility.Collapsed;
            Handle.ResizeGrip.Visibility = editable ? Visibility.Visible : Visibility.Collapsed;
            Root.Focusable = false;
            KeyboardNavigation.SetIsTabStop(Root, false);
            Panel.SetZIndex(Root, enabled ? selected ? 3 : 1 : 0);
            AutomationProperties.SetName(Root, enabled
                ? Title + " " + UiText.Get("layout preview frame") +
                  (hidden ? ", " + UiText.Get("hidden in gameplay") : string.Empty)
                : Title + " " + UiText.Get("HUD widget"));
            AutomationProperties.SetHelpText(Root, enabled && editable
                ? UiText.Get("Drag directly to move. Drag the corner to resize. Click to focus for Arrow and Shift plus Arrow adjustments.")
                : string.Empty);
            AutomationProperties.SetItemStatus(Root, hidden ? UiText.Get("Hidden in gameplay") : editable ? UiText.Get("Editable") : string.Empty);
            AutomationProperties.SetName(Handle.MoveThumb, Title + " " + UiText.Get("layout frame"));
            AutomationProperties.SetHelpText(Handle.MoveThumb, UiText.Get("Drag to move this frame. Arrow keys move; Shift plus Arrow resizes."));
            AutomationProperties.SetItemStatus(Handle.MoveThumb, hidden ? UiText.Get("Hidden in gameplay") : editable ? UiText.Get("Editable") : string.Empty);
        }
        private void RefreshEditorChromeVisibility()
        {
            if (Handle == null || Handle.EditorChrome == null)
            {
                return;
            }

            Handle.EditorChrome.Visibility = _editorMode && _pointerOver
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
        private void OnSelect(object sender, MouseButtonEventArgs args)
        {
            if (!_editorMode)
            {
                return;
            }

            RaiseSelected();
            Handle.MoveThumb.Focus();
        }

        private void OnKeyboardFocus(object sender, KeyboardFocusChangedEventArgs args)
        {
            if (_editorMode)
            {
                RaiseSelected();
            }
        }

        private void OnEditorKeyDown(object sender, KeyEventArgs args)
        {
            if (!_editorMode || !IsArrowKey(args.Key))
            {
                return;
            }

            var step = Keyboard.Modifiers.HasFlag(ModifierKeys.Control) ? 10d : 1d;
            var horizontal = args.Key == System.Windows.Input.Key.Left ? -step : args.Key == System.Windows.Input.Key.Right ? step : 0d;
            var vertical = args.Key == System.Windows.Input.Key.Up ? -step : args.Key == System.Windows.Input.Key.Down ? step : 0d;
            var resizing = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
            if (resizing)
            {
                Layout.Width += horizontal / _scale;
                Layout.Height += vertical / _scale;
            }
            else
            {
                Layout.X += horizontal;
                Layout.Y += vertical;
            }

            var canvas = VisualTreeHelper.GetParent(Root) as Canvas;
            if (canvas != null)
            {
                if (resizing)
                {
                    ApplyBounds(canvas, true, false);
                }
                else
                {
                    ApplyPositionBounds(canvas);
                }
            }

            RaiseLayoutChanged();
            args.Handled = true;
        }

        private static bool IsArrowKey(System.Windows.Input.Key key)
        {
            return key == System.Windows.Input.Key.Left || key == System.Windows.Input.Key.Right ||
                   key == System.Windows.Input.Key.Up || key == System.Windows.Input.Key.Down;
        }

        private void RaiseSelected()
        {
            var handler = Selected;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private void OnMove(object sender, DragDeltaEventArgs args)
        {
            if (!_editorMode)
            {
                return;
            }

            Layout.X += args.HorizontalChange;
            Layout.Y += args.VerticalChange;
            var canvas = VisualTreeHelper.GetParent(Root) as Canvas;
            if (canvas != null)
            {
                ApplyPositionBounds(canvas);
            }

            RaiseLayoutChanged();
        }

        private void OnResizeStarted(object sender, DragStartedEventArgs args)
        {
            _resizeActive = false;
            _resizeCanvas = VisualTreeHelper.GetParent(Root) as Canvas;
            if (_resizeCanvas == null)
            {
                return;
            }

            _resizeStartPointer = Mouse.GetPosition(_resizeCanvas);
            _resizeStartWidth = Root.Width;
            _resizeStartHeight = Root.Height;
            _resizeStartScale = _scale;
            _resizeActive = true;
        }

        private void OnResize(object sender, DragDeltaEventArgs args)
        {
            if (!_editorMode || !_resizeActive)
            {
                return;
            }

            var canvas = _resizeCanvas ?? VisualTreeHelper.GetParent(Root) as Canvas;
            if (canvas != null)
            {
                var pointer = Mouse.GetPosition(canvas);
                Layout.Width = _resizeStartWidth + (pointer.X - _resizeStartPointer.X) / _resizeStartScale;
                Layout.Height = _resizeStartHeight + (pointer.Y - _resizeStartPointer.Y) / _resizeStartScale;
                ApplyBounds(canvas, true, false);
            }

            RaiseLayoutChanged();
        }

        private void RaiseLayoutChanged()
        {
            var handler = LayoutChanged;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }
    }
}
