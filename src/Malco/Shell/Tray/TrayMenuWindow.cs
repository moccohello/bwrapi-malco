using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Malco.Localization;
using Malco.Settings.Views;
using Malco.Shell.Input;

namespace Malco.Shell.Tray
{
    internal sealed class TrayMenuWindow : Window
    {
        private readonly StackPanel _diagnostics;
        private readonly Button _settingsButton;
        private readonly Button _quitButton;
        private TextBlock _settingsLabel;
        private TextBlock _settingsHotkeyHint;
        private int _cursorX;
        private int _cursorY;
        private bool _closing;

        public TrayMenuWindow(
            string iconPath,
            IReadOnlyList<string> diagnostics,
            Action openSettings,
            Action requestQuit)
        {
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            SizeToContent = SizeToContent.WidthAndHeight;
            ShowInTaskbar = false;
            ShowActivated = true;
            Topmost = true;
            AllowsTransparency = true;
            Opacity = 0;
            Background = Brushes.Transparent;
            SnapsToDevicePixels = true;
            UseLayoutRounding = true;

            var border = new Border
            {
                Width = 272,
                Padding = new Thickness(8),
                CornerRadius = new CornerRadius(10),
                Background = Brush(SettingsVisualTokens.Panel),
                BorderBrush = Brush(SettingsVisualTokens.ControlBorder),
                BorderThickness = new Thickness(1),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius = 18,
                    Opacity = 0.42,
                    ShadowDepth = 5,
                    Color = Colors.Black
                }
            };
            var root = new StackPanel();
            border.Child = root;
            Content = border;

            root.Children.Add(CreateHeader(iconPath));
            root.Children.Add(CreateSeparator());
            _diagnostics = new StackPanel { Margin = new Thickness(0, 2, 0, 2) };
            root.Children.Add(_diagnostics);

            _settingsButton = CreateSettingsButton();
            _settingsButton.Click += (_, _) => openSettings();
            root.Children.Add(_settingsButton);

            _quitButton = CreateButton(UiText.Get("Quit Malco"));
            AutomationProperties.SetName(_quitButton, UiText.Get("Quit Malco"));
            _quitButton.Click += (_, _) => requestQuit();
            root.Children.Add(_quitButton);

            RefreshDiagnostics(diagnostics);
            Deactivated += (_, _) => Dismiss();
            PreviewKeyDown += OnPreviewKeyDown;
            SourceInitialized += OnSourceInitialized;
        }

        public void Dismiss()
        {
            if (_closing)
            {
                return;
            }

            _closing = true;
            Close();
        }

        public void RefreshText(
            IReadOnlyList<string> diagnostics,
            string settingsText,
            string quitText)
        {
            _settingsLabel.Text = settingsText;
            _quitButton.Content = quitText;
            AutomationProperties.SetName(_settingsButton, settingsText);
            AutomationProperties.SetName(_quitButton, quitText);
            RefreshDiagnostics(diagnostics);
        }

        public void ShowAt(int cursorX, int cursorY)
        {
            _cursorX = cursorX;
            _cursorY = cursorY;
            Show();
            Activate();
            _settingsButton.Focus();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            _closing = true;
            base.OnClosing(e);
        }

        private UIElement CreateHeader(string iconPath)
        {
            var panel = new DockPanel { Margin = new Thickness(8, 6, 8, 7) };
            if (!string.IsNullOrWhiteSpace(iconPath))
            {
                try
                {
                    var image = new BitmapImage();
                    using (var stream = new FileStream(iconPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        image.BeginInit();
                        image.CacheOption = BitmapCacheOption.OnLoad;
                        image.StreamSource = stream;
                        image.DecodePixelWidth = 26;
                        image.EndInit();
                    }
                    image.Freeze();
                    panel.Children.Add(new Image
                    {
                        Source = image,
                        Width = 26,
                        Height = 26,
                        Margin = new Thickness(0, 0, 10, 0)
                    });
                }
                catch
                {
                    // The product name remains a complete header if the optional image cannot be decoded.
                }
            }

            panel.Children.Add(new TextBlock
            {
                Text = UiText.Get("Malco"),
                Foreground = Brush(SettingsVisualTokens.TextPrimary),
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            });
            return panel;
        }

        private static Border CreateSeparator()
        {
            return new Border
            {
                Height = 1,
                Margin = new Thickness(5, 0, 5, 5),
                Background = Brush(SettingsVisualTokens.Separator)
            };
        }

        private Button CreateSettingsButton()
        {
            _settingsLabel = new TextBlock
            {
                Text = UiText.Get("Settings"),
                VerticalAlignment = VerticalAlignment.Center
            };
            _settingsHotkeyHint = new TextBlock
            {
                Text = HotkeyController.ShortcutDisplay,
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 11,
                Foreground = Brush(SettingsVisualTokens.TextSecondary),
                Visibility = Visibility.Collapsed
            };
            var row = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(_settingsHotkeyHint, Dock.Right);
            row.Children.Add(_settingsHotkeyHint);
            row.Children.Add(_settingsLabel);

            var button = CreateButton(row);
            button.MouseEnter += (_, _) => ShowSettingsHotkey(true);
            button.MouseLeave += (_, _) => ShowSettingsHotkey(false);
            AutomationProperties.SetName(button, UiText.Get("Settings"));
            AutomationProperties.SetHelpText(button, HotkeyController.ShortcutDisplay);
            return button;
        }

        private void ShowSettingsHotkey(bool visible)
        {
            _settingsHotkeyHint.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }

        private static Button CreateButton(object content)
        {
            var button = new Button
            {
                Content = content,
                Height = 36,
                Margin = new Thickness(0, 2, 0, 0),
                Padding = new Thickness(12, 0, 12, 0),
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Foreground = Brush(SettingsVisualTokens.TextPrimary),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                FontSize = 13,
                FocusVisualStyle = null,
                Template = CreateButtonTemplate()
            };
            return button;
        }

        private static ControlTemplate CreateButtonTemplate()
        {
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background")
            {
                RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)
            });
            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
            presenter.SetBinding(ContentPresenter.MarginProperty, new System.Windows.Data.Binding("Padding")
            {
                RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)
            });
            border.AppendChild(presenter);

            var template = new ControlTemplate(typeof(Button)) { VisualTree = border };
            var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hover.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, Brush(SettingsVisualTokens.HoverSurface)));
            template.Triggers.Add(hover);
            var focus = new Trigger { Property = UIElement.IsKeyboardFocusedProperty, Value = true };
            focus.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, Brush(SettingsVisualTokens.SelectedSurface)));
            template.Triggers.Add(focus);
            var pressed = new Trigger { Property = Button.IsPressedProperty, Value = true };
            pressed.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, Brush(SettingsVisualTokens.RaisedSurface)));
            template.Triggers.Add(pressed);
            return template;
        }

        private void RefreshDiagnostics(IReadOnlyList<string> diagnostics)
        {
            _diagnostics.Children.Clear();
            _diagnostics.Visibility = diagnostics.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            foreach (var diagnostic in diagnostics)
            {
                var row = new Border
                {
                    Margin = new Thickness(0, 1, 0, 1),
                    Padding = new Thickness(12, 8, 12, 8),
                    CornerRadius = new CornerRadius(6),
                    Background = Brush(SettingsVisualTokens.WarningSurface),
                    Child = new TextBlock
                    {
                        Text = diagnostic,
                        Foreground = Brush(SettingsVisualTokens.Warning),
                        FontSize = 11.5,
                        TextWrapping = TextWrapping.Wrap
                    }
                };
                AutomationProperties.SetName(row, diagnostic);
                AutomationProperties.SetHelpText(row, diagnostic);
                _diagnostics.Children.Add(row);
            }
        }

        private void OnSourceInitialized(object sender, EventArgs args)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(PositionWindow));
        }

        private void PositionWindow()
        {
            UpdateLayout();
            var handle = new WindowInteropHelper(this).Handle;
            // Move the hidden window to the target monitor first so Windows
            // applies that monitor's per-monitor DPI before size calculation.
            NativeMenuMethods.PositionWindow(handle, _cursorX, _cursorY);
            UpdateLayout();
            var dpi = NativeMenuMethods.GetDpiForWindow(handle);
            var scale = dpi == 0 ? 1d : dpi / 96d;
            var workArea = NativeMenuMethods.GetWorkArea(_cursorX, _cursorY);
            var widthPixels = ActualWidth * scale;
            var heightPixels = ActualHeight * scale;
            var leftPixels = Math.Min(_cursorX, workArea.Right - widthPixels - 8);
            var topPixels = Math.Min(_cursorY - heightPixels, workArea.Bottom - heightPixels - 8);
            leftPixels = Math.Max(workArea.Left + 8, leftPixels);
            topPixels = Math.Max(workArea.Top + 8, topPixels);
            NativeMenuMethods.PositionWindow(
                handle,
                (int)Math.Round(leftPixels),
                (int)Math.Round(topPixels));
            Opacity = 1;
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs args)
        {
            if (args.Key == Key.Escape)
            {
                Close();
                args.Handled = true;
            }
            else if (args.Key == Key.Down || args.Key == Key.Up)
            {
                var direction = args.Key == Key.Down
                    ? FocusNavigationDirection.Next
                    : FocusNavigationDirection.Previous;
                (Keyboard.FocusedElement as UIElement)?.MoveFocus(new TraversalRequest(direction));
                args.Handled = true;
            }
        }

        private static SolidColorBrush Brush(string value)
        {
            var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(value);
            brush.Freeze();
            return brush;
        }
    }
}
