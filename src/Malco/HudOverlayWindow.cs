using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Malco.Application.Projection;
using Malco.Bootstrap;
using Malco.Configuration;
using Malco.Configuration.Models;
using Malco.Localization;
using Malco.Models;
using Malco.Presentation;
using Malco.Presentation.Hud;
using Malco.Presentation.Hud.Buildings;
using Malco.Presentation.Hud.Tiles;
using Malco.Presentation.Hud.Units;
using Malco.Presentation.Hud.Upgrades;
using Malco.Presentation.Hud.Workers;
using Malco.Presentation.Scheduling;
using Malco.Presentation.Spatial;
using Malco.Settings.Contracts;
using Malco.Settings.Controller;
using Malco.Settings.Persistence;
using Malco.Settings.Views;
using Malco.Shell;
using Malco.Shell.Tray;

namespace Malco
{
    internal sealed partial class HudOverlayWindow : Window, ITrayIntentSink,
        IOverlayShellViewPort, ISettingsShellPort, IShellPresentationPort, ISettingsViewActions, IOverlaySceneViewPort
    {
        private const double HudReferenceWidth = 1280d;
        private const double HudReferenceHeight = 720d;
        private const double HudMinimumScale = 0.5d;
        private const double HudMaximumScale = 2d;

        internal static readonly SolidColorBrush TextBrush = FrozenBrush("#FFF3F6F8");
        internal static readonly SolidColorBrush MutedBrush = FrozenBrush("#FFA8B3BB");
        internal static readonly SolidColorBrush InkBrush = FrozenBrush("#FF070A0D");
        internal static readonly SolidColorBrush AmberBrush = FrozenBrush("#FFE1B94F");
        internal static readonly SolidColorBrush CoralBrush = FrozenBrush("#FFFF8A80");
        internal static readonly SolidColorBrush ChipBackgroundBrush = FrozenBrush("#90111A22");
        internal static readonly SolidColorBrush ChipBorderBrush = FrozenBrush("#80344451");
        internal static readonly SolidColorBrush SettingsTextBrush = FrozenBrush(SettingsVisualTokens.TextPrimary);
        internal static readonly SolidColorBrush SettingsMutedBrush = FrozenBrush(SettingsVisualTokens.TextSecondary);
        internal static readonly SolidColorBrush SettingsInkBrush = FrozenBrush(SettingsVisualTokens.OnAccent);
        internal static readonly SolidColorBrush SettingsAccentBrush = FrozenBrush(SettingsVisualTokens.Accent);
        internal static readonly SolidColorBrush SettingsWarningBrush = FrozenBrush(SettingsVisualTokens.Warning);
        internal static readonly SolidColorBrush SettingsWarningSurfaceBrush = FrozenBrush(SettingsVisualTokens.WarningSurface);
        internal static readonly SolidColorBrush SettingsDangerBrush = FrozenBrush(SettingsVisualTokens.Error);
        internal static readonly SolidColorBrush SettingsDangerSurfaceBrush = FrozenBrush(SettingsVisualTokens.ErrorSurface);
        internal static readonly SolidColorBrush SettingsPanelBrush = FrozenBrush(SettingsVisualTokens.Panel);
        internal static readonly SolidColorBrush SettingsSurfaceBrush = FrozenBrush(SettingsVisualTokens.Surface);
        internal static readonly SolidColorBrush SettingsRaisedSurfaceBrush = FrozenBrush(SettingsVisualTokens.RaisedSurface);
        internal static readonly SolidColorBrush SettingsHoverSurfaceBrush = FrozenBrush(SettingsVisualTokens.HoverSurface);
        internal static readonly SolidColorBrush SettingsSelectedSurfaceBrush = FrozenBrush(SettingsVisualTokens.SelectedSurface);
        internal static readonly SolidColorBrush SettingsBorderBrush = FrozenBrush(SettingsVisualTokens.ControlBorder);
        internal static readonly SolidColorBrush SettingsSeparatorBrush = FrozenBrush(SettingsVisualTokens.Separator);
        internal static readonly SolidColorBrush SettingsFocusBrush = FrozenBrush(SettingsVisualTokens.FocusRing);
        internal static readonly SolidColorBrush SettingsChipBackgroundBrush = FrozenBrush(SettingsVisualTokens.ChipBackground);
        internal static readonly SolidColorBrush SettingsChipBorderBrush = FrozenBrush(SettingsVisualTokens.ChipBorder);
        private static readonly SolidColorBrush EditorHitSurfaceBrush = FrozenBrush("#01000000");

        private OverlayRuntimeSessionHost _runtimeHost;
        private ProjectionPresentationAdapter _projectionPresentation;
        private LayoutLoadResult _layoutLoadResult;
        private SettingsController _settingsController;
        private SettingsPersistenceSession _settingsPersistence;
        private IconLocator _icons;
        private WorkersPresenter _workersPresenter;
        private HudTileFactory _hudTileFactory;
        private UnitsPresenter _unitsPresenter;
        private BuildingsPresenter _buildingsPresenter;
        private UpgradesPresenter _upgradesPresenter;
        private OverlayHudMetrics _hudMetrics;
        private HudVisualTree _hudVisualTree;
        private readonly Dictionary<string, HudWidgetView> _widgets = new Dictionary<string, HudWidgetView>(StringComparer.OrdinalIgnoreCase);
        private readonly ISettingsEditorChrome _settingsChrome;
        private readonly LayoutEditorView _layoutEditorView;
        private readonly FeatureSettingsView _featureSettingsView;
        private SpatialPresenter _spatialPresenter;
        private OverlayScenePresenter _scenePresenter;
        private OverlaySceneViewController _sceneViewController;
        private readonly DispatcherTimer _settingsStatusClock;
        private CompositionFramePump _framePump;
        private TrayController _trayController;
        private OverlayShellController _shellController;
        private bool _compositionBound;
        private bool _hudTemporarilyHidden;

        private readonly Grid _root = new Grid();
        private readonly Canvas _spatialCanvas = new Canvas();
        private readonly Canvas _hudCanvas = new Canvas();
        private readonly Border _editorPanel;
        private readonly Border _featurePanel;
        private TextBlock _editorStatus { get { return _layoutEditorView.Status; } }
        private readonly Button _settingsButton = new Button();

        private bool _editorMode;
        private bool _shutdownRequested;
        private bool _shutdownBlocked;
        private bool _shutdownPreparationComplete;
        private bool _resourcesDisposed;
        private bool _overlayPresentationVisible;
        private bool _initialVisibilityComplete;
        private bool _subscriptionsDetached;
        private HudDisplayPreferences _hudDisplayPreferences;
        private string _lastShellStatusText;
        private SettingsFlushResult? _lastPresentedSettingsFlush;
        private long _pendingSettingsRevision;
        private TextBlock _layoutSaveStatus { get { return _layoutEditorView.SaveStatus; } }
        private SettingsPage _activeEditorPage
        {
            get { return _settingsController.ActiveEditorPage; }
            set { _settingsController.ActiveEditorPage = value; }
        }
        private string _selectedWidgetKey
        {
            get { return _settingsController.SelectedWidgetKey; }
            set { _settingsController.SelectedWidgetKey = value; }
        }
        private Race _selectedTechTreeRace
        {
            get { return _settingsController.SelectedTechTreeRace; }
            set { _settingsController.SelectedTechTreeRace = value; }
        }
        public HudOverlayWindow(SettingsController settingsController)
        {
            _settingsController = settingsController ?? throw new ArgumentNullException(nameof(settingsController));
            var settingsPalette = new SettingsViewPalette
            {
                TextBrush = SettingsTextBrush,
                MutedBrush = SettingsMutedBrush,
                InkBrush = SettingsInkBrush,
                AccentBrush = SettingsAccentBrush,
                FocusBrush = SettingsFocusBrush,
                WarningBrush = SettingsWarningBrush,
                WarningSurfaceBrush = SettingsWarningSurfaceBrush,
                DangerBrush = SettingsDangerBrush,
                DangerSurfaceBrush = SettingsDangerSurfaceBrush,
                ChipBackgroundBrush = SettingsChipBackgroundBrush,
                ChipBorderBrush = SettingsChipBorderBrush,
                PanelBrush = SettingsPanelBrush,
                SurfaceBrush = SettingsSurfaceBrush,
                RaisedSurfaceBrush = SettingsRaisedSurfaceBrush,
                HoverSurfaceBrush = SettingsHoverSurfaceBrush,
                SelectedSurfaceBrush = SettingsSelectedSurfaceBrush,
                BorderBrush = SettingsBorderBrush,
                SeparatorBrush = SettingsSeparatorBrush,
                Text = SettingsText,
                GrayscaleIcon = GrayscaleIcon
            };
            _settingsChrome = new SettingsEditorChrome(settingsPalette);
            _layoutEditorView = new LayoutEditorView(
                this,
                _settingsChrome,
                settingsPalette,
                SettingsMutedBrush);
            _featureSettingsView = new FeatureSettingsView(this, _settingsChrome, settingsPalette);
            _settingsStatusClock = new DispatcherTimer(DispatcherPriority.Normal, Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(150d)
            };
            _settingsStatusClock.Tick += OnSettingsStatusClock;
            Title = UiText.Get("Malco");
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = true;
            ShowActivated = false;
            Opacity = 0d;
            Left = 0d;
            Top = 0d;
            Width = 1280d;
            Height = 720d;

            _root.Background = Brushes.Transparent;
            AutomationProperties.SetName(this, UiText.Get("Malco settings"));
            AutomationProperties.SetName(_root, UiText.Get("Malco settings"));
            _spatialCanvas.IsHitTestVisible = false;
            Panel.SetZIndex(_spatialCanvas, 10);
            Panel.SetZIndex(_hudCanvas, 20);
            _root.Children.Add(_spatialCanvas);
            _root.Children.Add(_hudCanvas);
            _hudCanvas.PreviewMouseLeftButtonDown += (sender, args) =>
            {
                var layoutEditing = _editorMode &&
                    _activeEditorPage == SettingsPage.Layout;
                if (layoutEditing && _layoutEditorView.HasPendingResetAll &&
                    !_layoutEditorView.IsResetConfirmationInteraction(args.OriginalSource as DependencyObject))
                {
                    _layoutEditorView.FocusResetConfirmation();
                    args.Handled = true;
                    return;
                }
                if (layoutEditing &&
                    ReferenceEquals(args.OriginalSource, _hudCanvas))
                {
                    _selectedWidgetKey = string.Empty;
                    ApplyWidgetEditorChrome();
                    _layoutEditorView.RefreshLayoutEditorState();
                    _layoutEditorView.FocusPrimaryControl();
                }
            };
            _hudCanvas.SizeChanged += OnHudCanvasSizeChanged;
            SizeChanged += OnOverlaySizeChanged;
            _editorPanel = _layoutEditorView.BuildEditorPanel();
            _featurePanel = _featureSettingsView.BuildPanel();
            _root.Children.Add(_editorPanel);
            _root.Children.Add(_featurePanel);
            Content = _root;
        }

        internal OverlayViewHandles ViewHandles
        {
            get
            {
                return new OverlayViewHandles(
                    _spatialCanvas,
                    _hudCanvas,
                    TextBrush,
                    MutedBrush,
                    AmberBrush,
                    CoralBrush,
                    ChipBackgroundBrush,
                    ChipBorderBrush,
                    GrayscaleIcon);
            }
        }

        internal void Bind(OverlayComposition composition)
        {
            if (_compositionBound) throw new InvalidOperationException("Overlay composition is already bound.");
            if (composition == null) throw new ArgumentNullException(nameof(composition));

            _runtimeHost = composition.RuntimeHost;
            _projectionPresentation = composition.ProjectionPresentation;
            _layoutLoadResult = composition.LayoutLoadResult;
            _settingsController = composition.SettingsController;
            _settingsPersistence = composition.SettingsPersistence;
            _icons = composition.Icons;
            _featureSettingsView.SetIconLocator(_icons);
            _hudTileFactory = composition.HudTileFactory;
            _workersPresenter = composition.WorkersPresenter;
            _unitsPresenter = composition.UnitsPresenter;
            _buildingsPresenter = composition.BuildingsPresenter;
            _upgradesPresenter = composition.UpgradesPresenter;
            _hudMetrics = composition.HudMetrics;
            _hudVisualTree = composition.HudVisualTree;
            _spatialPresenter = composition.SpatialPresenter;
            _scenePresenter = composition.ScenePresenter;
            _sceneViewController = composition.SceneViewController;
            _framePump = composition.FramePump;
            _trayController = composition.TrayController;
            _shellController = composition.ShellController;
            _hudDisplayPreferences = HudDisplayPreferences.FromLayout(
                _settingsController.Capture().Snapshot.ToMutable());
            _compositionBound = true;

            BuildWidgets();
            RenderEditorTab();
            ApplyEditorMode(false);
            ApplyInitialLayoutLoadStatus();
            SourceInitialized += OnSourceInitialized;
            Closing += OnClosing;
            Closed += OnClosed;
            PreviewKeyDown += OnPreviewKeyDown;
            DpiChanged += OnOverlayDpiChanged;
            _runtimeHost.Start();
        }

        private static string ProgramVersion
        {
            get
            {
                var assembly = typeof(HudOverlayWindow).Assembly;
                var informational = assembly
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                    ?.InformationalVersion;
                return string.IsNullOrWhiteSpace(informational)
                    ? assembly.GetName().Version?.ToString() ?? "unknown"
                    : informational;
            }
        }

        internal void ToggleSettingsFromShell()
        {
            HandleSettingsIntent(new SettingsIntent(SettingsIntentKind.ToggleEditor));
        }

        internal void ApplyCompositionFrame()
        {
            OnRendering(null, EventArgs.Empty);
            _sceneViewController.RefreshFramePumpArming();
        }

        internal bool ResourcesDisposed { get { return _resourcesDisposed; } }
        internal bool ShutdownBlocked { get { return _shutdownBlocked; } }

        internal void DetachSubscriptionsForFallback()
        {
            DetachWindowSubscriptions();
        }

        internal void MarkFallbackResourcesDisposed() { _resourcesDisposed = true; }

        internal void HideOverlayForRuntimeShutdown() => SetOverlayPresentation(false);

        private void DetachWindowSubscriptions()
        {
            if (_subscriptionsDetached || !_compositionBound) return;
            _subscriptionsDetached = true;
            _settingsStatusClock.Stop();
            _settingsStatusClock.Tick -= OnSettingsStatusClock;
            SourceInitialized -= OnSourceInitialized;
            Closing -= OnClosing;
            Closed -= OnClosed;
            PreviewKeyDown -= OnPreviewKeyDown;
            DpiChanged -= OnOverlayDpiChanged;
        }
    }
}
