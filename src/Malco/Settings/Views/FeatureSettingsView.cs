using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Malco.Localization;
using Malco.Settings.Contracts;

namespace Malco.Settings.Views
{
    internal sealed partial class FeatureSettingsView
    {
        private readonly ISettingsViewActions _actions;
        private readonly ISettingsEditorChrome _chrome;
        private readonly SettingsViewPalette _palette;
        private readonly FeatureSettingsPreviewFactory _previewFactory;
        private readonly StackPanel _navigationHost = new StackPanel();
        private readonly StackPanel _detailHost = new StackPanel();
        private readonly ScrollViewer _detailScroll = new ScrollViewer();
        private Grid _shell;
        private ColumnDefinition _navigationColumn;
        private StackPanel _itemListHost;
        private TextBox _searchInput;
        private FrameworkElement _pendingFocusTarget;
        private RoutedEventHandler _pendingFocusLoadedHandler;
        private Border _saveErrorBanner;
        private TextBlock _saveErrorMessage;
        private Button _retrySaveButton;
        private Button _temporaryHudButton;
        private TextBlock _headerTitle;
        private TextBlock _headerSubtitle;
        private Button _arrangeButton;
        private Button _closeButton;
        private string _selectedFeatureKey = HudWidgetRegistry.Workers;
        private string _searchText = string.Empty;
        private bool _compactLayout;
        private bool _ultraCompactLayout;

        public FeatureSettingsView(ISettingsViewActions actions, ISettingsEditorChrome chrome, SettingsViewPalette palette)
        {
            _actions = actions ?? throw new ArgumentNullException(nameof(actions));
            _chrome = chrome ?? throw new ArgumentNullException(nameof(chrome));
            _palette = palette ?? throw new ArgumentNullException(nameof(palette));
            _previewFactory = new FeatureSettingsPreviewFactory(_palette);
            SaveStatus = _palette.Text(UiText.Get("Saved"), 12d, FontWeights.SemiBold, _palette.MutedBrush);
            AutomationProperties.SetLiveSetting(SaveStatus, AutomationLiveSetting.Polite);
        }

        public Border Panel { get; private set; }
        public Button FocusButton { get; private set; }
        public TextBlock SaveStatus { get; }

        public void SetIconLocator(IconLocator icons)
        {
            _previewFactory.SetIconLocator(icons);
        }

        public void SetSaveStatus(string text, string automationState, Brush foreground)
        {
            SaveStatus.Text = text ?? string.Empty;
            SaveStatus.Foreground = foreground;
            AutomationProperties.SetName(SaveStatus, UiText.Get("Settings") + ": " + (automationState ?? string.Empty));
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

        public bool FocusPrimaryControl()
        {
            var target = (FrameworkElement)_searchInput ?? FocusButton;
            if (target == null)
            {
                return false;
            }

            FocusWhenLoaded(target);
            return true;
        }

        public void UpdateResponsiveLayout(double width)
        {
            var compact = width > 0d && width < 960d;
            var ultraCompact = width > 0d && width < 700d;
            if (_shell == null || _navigationColumn == null)
            {
                return;
            }

            var modeChanged = compact != _compactLayout || ultraCompact != _ultraCompactLayout;
            _compactLayout = compact;
            _ultraCompactLayout = ultraCompact;
            _shell.Margin = new Thickness(ultraCompact ? 8d : compact ? 16d : 32d);
            _navigationColumn.Width = new GridLength(ultraCompact ? 168d : compact ? 200d : 260d);
            _detailScroll.Padding = ultraCompact
                ? new Thickness(12d, 12d, 12d, 16d)
                : compact
                    ? new Thickness(16d, 16d, 16d, 20d)
                    : new Thickness(24d, 20d, 24d, 24d);
            if (modeChanged)
            {
                var restoreFeatureFocus = _navigationHost.IsKeyboardFocusWithin || _detailHost.IsKeyboardFocusWithin;
                var focusedElement = restoreFeatureFocus ? Keyboard.FocusedElement as FrameworkElement : null;
                var focusedAutomationName = focusedElement == null
                    ? string.Empty
                    : AutomationProperties.GetName(focusedElement);
                Refresh();
                if (restoreFeatureFocus)
                {
                    RestoreFocusAfterRender(focusedAutomationName, true);
                }
            }
        }

        public void OpenFeature(string key)
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                _selectedFeatureKey = HudFeatureCatalog.NormalizeSelectionKey(key);
            }
            _searchText = string.Empty;
        }

        public Border BuildPanel()
        {
            _compactLayout = _actions.ViewWidth > 0d && _actions.ViewWidth < 960d;
            _ultraCompactLayout = _actions.ViewWidth > 0d && _actions.ViewWidth < 700d;
            _shell = new Grid
            {
                Background = Brushes.Transparent,
                Margin = new Thickness(_ultraCompactLayout ? 8d : _compactLayout ? 16d : 32d)
            };
            _shell.PreviewMouseLeftButtonDown += (sender, args) =>
            {
                if (ReferenceEquals(args.OriginalSource, _shell))
                {
                    _actions.Dispatch(new SettingsIntent(SettingsIntentKind.ReturnToGame));
                    args.Handled = true;
                }
            };

            var modal = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                MaxWidth = 1120d,
                MaxHeight = 760d,
                Background = _palette.PanelBrush,
                BorderBrush = _palette.BorderBrush,
                BorderThickness = new Thickness(1d),
                CornerRadius = new CornerRadius(4d)
            };
            var modalGrid = new Grid();
            modalGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(72d) });
            modalGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            modalGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1d, GridUnitType.Star) });
            var header = BuildHeader();
            Grid.SetRow(header, 0);
            modalGrid.Children.Add(header);
            _saveErrorBanner = BuildSaveErrorBanner();
            Grid.SetRow(_saveErrorBanner, 1);
            modalGrid.Children.Add(_saveErrorBanner);

            var body = new Grid();
            _navigationColumn = new ColumnDefinition
            {
                Width = new GridLength(_ultraCompactLayout ? 168d : _compactLayout ? 200d : 260d)
            };
            body.ColumnDefinitions.Add(_navigationColumn);
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
            var navigationScroll = new ScrollViewer
            {
                Content = _navigationHost,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(12d, 16d, 12d, 16d)
            };
            _chrome.ConfigureScrollViewer(navigationScroll);
            var navigation = new Border
            {
                Background = _palette.SurfaceBrush,
                BorderBrush = _palette.SeparatorBrush,
                BorderThickness = new Thickness(0d, 0d, 1d, 0d),
                Child = navigationScroll
            };
            Grid.SetColumn(navigation, 0);
            body.Children.Add(navigation);

            _detailScroll.Content = _detailHost;
            _detailScroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            _detailScroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            _detailScroll.Padding = _ultraCompactLayout
                ? new Thickness(12d, 12d, 12d, 16d)
                : _compactLayout
                    ? new Thickness(16d, 16d, 16d, 20d)
                    : new Thickness(24d, 20d, 24d, 24d);
            _chrome.ConfigureScrollViewer(_detailScroll);
            Grid.SetColumn(_detailScroll, 1);
            body.Children.Add(_detailScroll);
            Grid.SetRow(body, 2);
            modalGrid.Children.Add(body);
            modal.Child = modalGrid;
            _shell.Children.Add(modal);

            Panel = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(SettingsVisualTokens.Backdrop)),
                Child = _shell,
                Visibility = Visibility.Collapsed
            };
            Panel.PreviewMouseLeftButtonDown += (sender, args) =>
            {
                if (ReferenceEquals(args.OriginalSource, Panel))
                {
                    _actions.Dispatch(new SettingsIntent(SettingsIntentKind.ReturnToGame));
                    args.Handled = true;
                }
            };
            AutomationProperties.SetName(Panel, UiText.Get("Malco settings"));
            System.Windows.Controls.Panel.SetZIndex(Panel, 110);
            return Panel;
        }

        private Border BuildSaveErrorBanner()
        {
            var content = new Grid { Margin = new Thickness(16d, 10d, 16d, 10d) };
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32d) });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var marker = _palette.Text("!", 18d, FontWeights.Bold, _palette.DangerBrush);
            marker.VerticalAlignment = VerticalAlignment.Top;
            content.Children.Add(marker);
            _saveErrorMessage = _palette.Text(string.Empty, 12d, FontWeights.SemiBold, _palette.TextBrush);
            _saveErrorMessage.TextWrapping = TextWrapping.Wrap;
            _saveErrorMessage.TextTrimming = TextTrimming.None;
            AutomationProperties.SetLiveSetting(_saveErrorMessage, AutomationLiveSetting.Assertive);
            Grid.SetColumn(_saveErrorMessage, 1);
            content.Children.Add(_saveErrorMessage);
            _retrySaveButton = _chrome.ActionButton(UiText.Get("Retry save"));
            _retrySaveButton.Height = 44d;
            _retrySaveButton.Margin = new Thickness(16d, 0d, 0d, 0d);
            _retrySaveButton.Click += (sender, args) => _actions.RetrySettingsSave();
            Grid.SetColumn(_retrySaveButton, 2);
            content.Children.Add(_retrySaveButton);
            return new Border
            {
                Visibility = Visibility.Collapsed,
                Background = _palette.DangerSurfaceBrush,
                BorderBrush = _palette.DangerBrush,
                BorderThickness = new Thickness(4d, 0d, 0d, 1d),
                Child = content
            };
        }

        public void Refresh()
        {
            RefreshTemporaryHudState();
            RenderNavigation();
            RenderDetail();
        }

        public void RefreshLanguage()
        {
            if (Panel != null)
            {
                AutomationProperties.SetName(Panel, UiText.Get("Malco settings"));
            }
            if (_headerTitle != null)
            {
                _headerTitle.Text = UiText.Get("Malco");
            }
            if (_headerSubtitle != null)
            {
                _headerSubtitle.Text = UiText.Get("HUD settings");
            }
            if (_arrangeButton != null)
            {
                _arrangeButton.Content = UiText.Get("Arrange HUD");
                AutomationProperties.SetName(_arrangeButton, UiText.Get("Arrange HUD"));
            }
            if (_closeButton != null)
            {
                _closeButton.ToolTip = UiText.Get("Return to Game");
                AutomationProperties.SetName(_closeButton, UiText.Get("Return to game and close overlay settings"));
            }
            if (_retrySaveButton != null)
            {
                _retrySaveButton.Content = UiText.Get("Retry save");
                AutomationProperties.SetName(_retrySaveButton, UiText.Get("Retry save"));
            }
            Refresh();
        }

        public void RefreshTemporaryHudState()
        {
            if (_temporaryHudButton == null)
            {
                return;
            }

            var hidden = _actions.HudTemporarilyHidden;
            var label = UiText.Get(hidden ? "Show HUD again" : "Hide HUD");
            var help = UiText.Get(hidden
                ? "Show the HUD again. Your saved settings are unchanged."
                : "Hide the entire HUD for this session. It is shown again after Malco restarts.");
            _temporaryHudButton.Content = label;
            _temporaryHudButton.ToolTip = help;
            _temporaryHudButton.Tag = hidden ? "Warning" : null;
            AutomationProperties.SetName(_temporaryHudButton, label);
            AutomationProperties.SetHelpText(_temporaryHudButton, help);
            AutomationProperties.SetItemStatus(
                _temporaryHudButton,
                UiText.Get(hidden ? "HUD temporarily hidden" : "HUD visible"));
        }

        private FrameworkElement BuildHeader()
        {
            var header = new Grid
            {
                Background = _palette.RaisedSurfaceBrush,
                Margin = new Thickness(1d, 1d, 1d, 0d)
            };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var identity = new StackPanel
            {
                Margin = new Thickness(24d, 12d, 12d, 0d),
                VerticalAlignment = VerticalAlignment.Top
            };
            _headerTitle = _palette.Text(UiText.Get("Malco"), 20d, FontWeights.Bold, _palette.TextBrush);
            _headerSubtitle = _palette.Text(UiText.Get("HUD settings"), 12d, FontWeights.SemiBold, _palette.MutedBrush);
            identity.Children.Add(_headerTitle);
            identity.Children.Add(_headerSubtitle);
            header.Children.Add(identity);

            SaveStatus.Margin = new Thickness(12d, 0d, 16d, 0d);
            SaveStatus.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(SaveStatus, 1);
            header.Children.Add(SaveStatus);

            _temporaryHudButton = _chrome.ActionButton(string.Empty);
            _temporaryHudButton.Height = 44d;
            _temporaryHudButton.MinWidth = 128d;
            _temporaryHudButton.Margin = new Thickness(0d, 0d, 8d, 0d);
            _temporaryHudButton.Click += (sender, args) =>
            {
                _actions.SetHudTemporarilyHidden(!_actions.HudTemporarilyHidden);
                _actions.Dispatch(new SettingsIntent(SettingsIntentKind.ReturnToGame));
            };
            Grid.SetColumn(_temporaryHudButton, 2);
            header.Children.Add(_temporaryHudButton);
            RefreshTemporaryHudState();

            _arrangeButton = _chrome.ActionButton(UiText.Get("Arrange HUD"));
            _arrangeButton.Height = 44d;
            _arrangeButton.MinWidth = 110d;
            _arrangeButton.Margin = new Thickness(0d, 0d, 8d, 0d);
            _arrangeButton.Click += (sender, args) => OpenLayoutForSelectedFeature();
            Grid.SetColumn(_arrangeButton, 3);
            header.Children.Add(_arrangeButton);

            _closeButton = _chrome.ActionButton("\u2715");
            _closeButton.Width = 44d;
            _closeButton.MinWidth = 44d;
            _closeButton.Height = 44d;
            _closeButton.Margin = new Thickness(0d, 0d, 16d, 0d);
            _closeButton.Padding = new Thickness(0d);
            _closeButton.ToolTip = UiText.Get("Return to Game");
            AutomationProperties.SetName(_closeButton, UiText.Get("Return to game and close overlay settings"));
            _closeButton.Click += (sender, args) => _actions.Dispatch(new SettingsIntent(SettingsIntentKind.ReturnToGame));
            Grid.SetColumn(_closeButton, 4);
            header.Children.Add(_closeButton);
            return header;
        }
    }
}
