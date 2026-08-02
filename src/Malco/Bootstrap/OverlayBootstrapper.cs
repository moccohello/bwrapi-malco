using System;
using System.Collections.Generic;
using System.Windows.Threading;
using Malco.Application.Projection;
using Malco.Application.Overlay;
using Malco.Configuration;
using Malco.Data;
using Malco.Game.Services;
using Malco.Presentation;
using Malco.Presentation.Hud;
using Malco.Presentation.Hud.Buildings;
using Malco.Presentation.Hud.Tiles;
using Malco.Presentation.Hud.Units;
using Malco.Presentation.Hud.Upgrades;
using Malco.Presentation.Hud.Workers;
using Malco.Presentation.Scheduling;
using Malco.Presentation.Spatial;
using Malco.Settings.Controller;
using Malco.Settings.Persistence;
using Malco.Shell;
using Malco.Shell.Control;
using Malco.Shell.Input;
using Malco.Shell.Shutdown;
using Malco.Shell.Tray;
using Malco.Localization;
using Malco.Integration.Telemetry;
using Malco.Telemetry;
using Malco.Updates;

namespace Malco.Bootstrap
{
    internal sealed class OverlayBootstrapper : IDisposable
    {
        private readonly InstalledLaunchAuthorization _installedLaunchAuthorization;
        private HudOverlayWindow _window;
        private OverlayComposition _composition;
        private BwrApiEmbeddedRuntimeProvider _provider;
        private IDisposable _providerPendingOwnership;
        private IGameDataProviderLifecycle _providerPendingLifecycle;
        private TimeSpan _providerShutdownTimeout = TimeSpan.FromMilliseconds(OverlayConfig.RuntimeProviderShutdownTimeoutMs);
        private OverlayShutdownResult _constructionShutdownResult =
            new OverlayShutdownResult(OverlayShutdownStatus.Complete, string.Empty);
        private GameCoordinator _coordinator;
        private OverlayShellController _shell;
        private CompositionFramePump _framePump;
        private ProjectionCommitSubscription _projectionCommitSubscription;
        private readonly Stack<IDisposable> _constructionCleanup = new Stack<IDisposable>();
        private bool _disposed;

        public OverlayBootstrapper(InstalledLaunchAuthorization installedLaunchAuthorization)
        {
            _installedLaunchAuthorization = installedLaunchAuthorization;
        }

        public HudOverlayWindow CreateWindow()
        {
            if (_window != null) throw new InvalidOperationException("The overlay window has already been created.");
            try
            {
            var config = new OverlayConfig();
            config.Normalize();
            _providerShutdownTimeout = TimeSpan.FromMilliseconds(
                Math.Max(1, config.ProviderShutdownTimeoutMs));
            var layoutStore = new HudLayoutFileStore(AppPaths.UserLayoutPath);
            var layoutLoadResult = layoutStore.Load();
            UiText.Initialize(layoutLoadResult.Layout.Language);
            var settingsController = new SettingsController(layoutLoadResult.Layout);
            var settingsPersistence = new SettingsPersistenceSession(settingsController, layoutStore);
            Capture(settingsPersistence);

            var telemetryClient = _installedLaunchAuthorization != null
                ? TelemetryClient.TryCreate(
                    System.IO.Path.Combine(AppContext.BaseDirectory, "telemetry-policy.json"),
                    AppPaths.UserDataDirectory,
                    typeof(OverlayBootstrapper).Assembly.GetName().Version?.ToString() ?? string.Empty)
                : null;
            var telemetry = new MalcoTelemetryIntegration(
                telemetryClient,
                () => settingsController.Capture().Snapshot);
            Capture(telemetry);

            var window = new HudOverlayWindow(settingsController);
            _window = window;
            var view = window.ViewHandles;
            var icons = new IconLocator();
            var hudTileFactory = new HudTileFactory(
                icons,
                view.TextBrush,
                view.AmberBrush,
                view.ChipBackgroundBrush,
                view.ChipBorderBrush,
                view.GrayscaleIcon);
            var workers = new WorkersPresenter(icons, view.TextBrush);
            var units = new UnitsPresenter(hudTileFactory, view.MutedBrush);
            var buildings = new BuildingsPresenter(hudTileFactory, view.MutedBrush);
            var upgradeTiles = new UpgradeTileFactory(
                hudTileFactory,
                view.TextBrush,
                view.MutedBrush,
                view.AmberBrush,
                view.ChipBackgroundBrush,
                view.ChipBorderBrush);
            var upgrades = new UpgradesPresenter(
                new CompletedUpgradesPresenter(upgradeTiles, view.MutedBrush),
                new UpgradeWarningsPresenter(upgradeTiles),
                new AvailableUpgradesPresenter(upgradeTiles));
            var spatial = new SpatialPresenter(
                new SpatialVisualTree(view.SpatialCanvas),
                new SpatialVisualStyle(
                    view.ChipBackgroundBrush,
                    view.ChipBorderBrush,
                    view.TextBrush,
                    view.CoralBrush),
                icons);

            _provider = new BwrApiEmbeddedRuntimeProvider(config);
            _providerPendingOwnership = _provider as IDisposable;
            var providerLifecycle = _provider as IGameDataProviderLifecycle;
            if (providerLifecycle == null)
            {
                throw new InvalidOperationException("The BWRAPI observer does not expose its lifecycle contract.");
            }
            _providerPendingLifecycle = providerLifecycle;
            providerLifecycle.Start();
            var coordinator = new GameCoordinator(
                providerLifecycle,
                _provider,
                _provider,
                _provider,
                _provider,
                config.ProviderShutdownTimeoutMs);
            _coordinator = coordinator;
            _providerPendingLifecycle = null;
            _providerPendingOwnership = null;
            Capture(coordinator);
            var requiredUpdateMonitor =
                _installedLaunchAuthorization?.RequiredUpdateRecheck == true
                    ? new RequiredUpdateSessionMonitor(
                        coordinator,
                        _installedLaunchAuthorization.LauncherPath,
                        _installedLaunchAuthorization.ManifestSha256,
                        _installedLaunchAuthorization.LauncherProcessId,
                        _installedLaunchAuthorization.LauncherStartTimeUtcTicks,
                        window.Dispatcher,
                        window.RequestApplicationShutdown)
                    : null;
            Capture(requiredUpdateMonitor);
            var applicationController = new OverlayApplicationController(coordinator);
            var projectionPresentation = new ProjectionPresentationAdapter(coordinator.ProjectionMailboxReader);
            var clock = new DispatcherTimer(DispatcherPriority.Background, window.Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(100d)
            };
            var framePump = new CompositionFramePump(window.Dispatcher, window.ApplyCompositionFrame);
            _framePump = framePump;
            Capture(framePump);
            var projectionCommitSubscription = new ProjectionCommitSubscription(coordinator, framePump);
            _projectionCommitSubscription = projectionCommitSubscription;
            Capture(projectionCommitSubscription);
            var hotkey = new HotkeyController(window.ToggleSettingsFromShell);
            Capture(hotkey);
            var tray = new TrayController(window.Dispatcher, window);
            Capture(tray);
            var shell = new OverlayShellController(config, window, window, window, hotkey, framePump, _provider);
            _shell = shell;
            Capture(shell);
            var metrics = OverlayHudMetrics.CreateFromEnvironment(
                _provider as IProviderOptimizationMetricsSource,
                framePump);
            Capture(metrics);
            var scenePresenter = new OverlayScenePresenter();
            var sceneViewController = new OverlaySceneViewController(
                window,
                coordinator,
                applicationController,
                coordinator,
                projectionPresentation,
                workers,
                units,
                buildings,
                upgrades,
                spatial,
                scenePresenter,
                metrics,
                shell,
                clock,
                framePump);
            var scheduler = new DispatcherPresentationScheduler(window.Dispatcher, sceneViewController.DrainPresentation);
            var controlServer = new MalcoControlServer(window.Dispatcher, window);
            Capture(controlServer);
            var applicationSession = new OverlayApplicationSession(
                framePump,
                metrics,
                settingsPersistence,
                telemetry,
                requiredUpdateMonitor,
                hotkey,
                shell,
                controlServer,
                tray);
            Capture(applicationSession);

            _composition = new OverlayComposition
            {
                ApplicationSession = applicationSession,
                Coordinator = coordinator,
                ApplicationController = applicationController,
                ProjectionPresentation = projectionPresentation,
                LayoutLoadResult = layoutLoadResult,
                SettingsController = settingsController,
                SettingsPersistence = settingsPersistence,
                Telemetry = telemetry,
                Icons = icons,
                HudTileFactory = hudTileFactory,
                WorkersPresenter = workers,
                UnitsPresenter = units,
                BuildingsPresenter = buildings,
                UpgradesPresenter = upgrades,
                HudMetrics = metrics,
                HudVisualTree = new HudVisualTree(view.HudCanvas),
                SpatialPresenter = spatial,
                ScenePresenter = scenePresenter,
                SceneViewController = sceneViewController,
                PresentationScheduler = scheduler,
                PresentationClock = clock,
                FramePump = framePump,
                ProjectionCommitSubscription = projectionCommitSubscription,
                ShutdownController = new OverlayShutdownController(),
                HotkeyController = hotkey,
                TrayController = tray,
                ControlServer = controlServer,
                ShellController = shell
            };
            window.Bind(_composition);
            requiredUpdateMonitor?.Start();
            controlServer.Start();
            return window;
            }
            catch (Exception ex)
            {
                if (!CleanupConstructionFailure())
                {
                    throw new InvalidOperationException(
                        ex.Message + " " + _constructionShutdownResult.Message,
                        ex);
                }
                throw;
            }
        }

        internal OverlayShutdownResult ConstructionShutdownResult
        {
            get { return _constructionShutdownResult; }
        }

        public void Dispose()
        {
            if (_disposed) return;
            if (_composition == null || _window == null)
            {
                if (CleanupConstructionFailure()) _disposed = true;
                return;
            }
            if (_window.ResourcesDisposed)
            {
                _constructionCleanup.Clear();
                _disposed = true;
                return;
            }
            _composition.ShellController.PrepareForRuntimeShutdown();
            _composition.ControlServer.Dispose();
            _composition.ProjectionCommitSubscription.Dispose();
            _composition.FramePump.Stop();
            _composition.Coordinator.UnregisterStateCommitSink(_composition.PresentationScheduler);
            if (_composition.Telemetry != null)
                _composition.Coordinator.UnregisterStateCommitSink(_composition.Telemetry);
            _composition.PresentationScheduler.Stop();
            _composition.PresentationClock.Stop();
            _window.DetachSubscriptionsForFallback();
            var shutdown = _composition.ShutdownController.TryStopApplication(_composition.Coordinator);
            if (!shutdown.IsComplete) return;

            _window.MarkFallbackResourcesDisposed();
            DisposeCaptured();
            _disposed = true;
        }

        private void Capture(IDisposable disposable)
        {
            if (disposable != null) _constructionCleanup.Push(disposable);
        }

        private bool CleanupConstructionFailure()
        {
            try { _shell?.PrepareForRuntimeShutdown(); }
            catch { }
            try { _projectionCommitSubscription?.Dispose(); }
            catch { }
            try { _framePump?.Stop(); }
            catch { }
            if (_window != null) _window.DetachSubscriptionsForFallback();
            if (_coordinator != null)
            {
                try
                {
                    var result = new OverlayShutdownController().TryStopApplication(_coordinator);
                    if (!result.IsComplete)
                    {
                        _constructionShutdownResult = new OverlayShutdownResult(
                            OverlayShutdownStatus.Blocked,
                            result.Message + " Disposing the bootstrapper retries construction cleanup.");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    SetConstructionShutdownBlocked("Coordinator construction cleanup failed: " + ex.Message);
                    return false;
                }
            }
            else if (_providerPendingLifecycle != null)
            {
                ProviderStopResult stop;
                try
                {
                    _providerPendingLifecycle.BeginStop();
                    stop = _providerPendingLifecycle.TryStop(_providerShutdownTimeout);
                }
                catch (Exception ex)
                {
                    SetConstructionShutdownBlocked("Provider construction cleanup failed: " + ex.Message);
                    return false;
                }

                if (!stop.IsComplete)
                {
                    SetConstructionShutdownBlocked(string.IsNullOrWhiteSpace(stop.Message)
                        ? "Provider construction cleanup remains blocked."
                        : stop.Message);
                    return false;
                }

                _providerPendingLifecycle = null;
                _providerPendingOwnership = null;
            }
            else if (_providerPendingOwnership != null)
            {
                SafeDispose(_providerPendingOwnership);
                _providerPendingOwnership = null;
            }
            DisposeCaptured();
            if (_window != null) _window.MarkFallbackResourcesDisposed();
            _constructionShutdownResult = new OverlayShutdownResult(
                OverlayShutdownStatus.Complete,
                string.Empty);
            return true;
        }

        private void SetConstructionShutdownBlocked(string message)
        {
            _constructionShutdownResult = new OverlayShutdownResult(
                OverlayShutdownStatus.Blocked,
                (message ?? "Construction cleanup is blocked.") +
                " Disposing the bootstrapper retries construction cleanup.");
        }

        private void DisposeCaptured()
        {
            while (_constructionCleanup.Count != 0) SafeDispose(_constructionCleanup.Pop());
        }

        private static void SafeDispose(IDisposable disposable)
        {
            try { disposable?.Dispose(); }
            catch { }
        }
    }
}
