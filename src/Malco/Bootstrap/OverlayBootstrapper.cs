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
        private OverlayRuntimeSessionHost _runtimeHost;
        private IDisposable _providerPendingOwnership;
        private IGameDataProviderLifecycle _providerPendingLifecycle;
        private string _constructionShutdownFailureMessage = string.Empty;
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

            var provider = new BwrApiEmbeddedRuntimeProvider(config);
            _providerPendingOwnership = provider;
            var providerLifecycle = (IGameDataProviderLifecycle)provider;
            _providerPendingLifecycle = providerLifecycle;
            providerLifecycle.Start();
            var coordinator = new GameCoordinator(
                providerLifecycle,
                provider,
                provider,
                provider,
                provider,
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
            var shell = new OverlayShellController(config, window, window, window, hotkey, framePump, provider);
            _shell = shell;
            Capture(shell);
            var metrics = OverlayHudMetrics.CreateFromEnvironment(
                provider,
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
            var runtimeHost = new OverlayRuntimeSessionHost(
                coordinator,
                telemetry,
                scheduler,
                clock,
                projectionCommitSubscription,
                framePump,
                shell,
                new OverlayShutdownController(),
                applicationSession,
                window.HideOverlayForRuntimeShutdown);
            _runtimeHost = runtimeHost;
            _composition = new OverlayComposition
            {
                RuntimeHost = runtimeHost,
                ProjectionPresentation = projectionPresentation,
                LayoutLoadResult = layoutLoadResult,
                SettingsController = settingsController,
                SettingsPersistence = settingsPersistence,
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
                FramePump = framePump,
                TrayController = tray,
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
                        ex.Message + " " + _constructionShutdownFailureMessage,
                        ex);
                }
                throw;
            }
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
            _runtimeHost.BeginShutdown();
            _window.DetachSubscriptionsForFallback();
            var shutdown = _runtimeHost.TryStop();
            if (!shutdown.IsComplete) return;

            _window.MarkFallbackResourcesDisposed();
            _runtimeHost.Complete();
            _constructionCleanup.Clear();
            _disposed = true;
        }

        private void Capture(IDisposable disposable)
        {
            if (disposable != null) _constructionCleanup.Push(disposable);
        }

        private bool CleanupConstructionFailure()
        {
            if (_runtimeHost != null)
            {
                try
                {
                    _runtimeHost.BeginShutdown();
                    _window?.DetachSubscriptionsForFallback();
                    var shutdown = _runtimeHost.TryStop();
                    if (!shutdown.IsComplete)
                    {
                        _constructionShutdownFailureMessage =
                            shutdown.Message + " Disposing the bootstrapper retries construction cleanup.";
                        return false;
                    }
                    _runtimeHost.Complete();
                    _constructionCleanup.Clear();
                    _window?.MarkFallbackResourcesDisposed();
                    _constructionShutdownFailureMessage = string.Empty;
                    return true;
                }
                catch (Exception ex)
                {
                    SetConstructionShutdownBlocked("Runtime construction cleanup failed: " + ex.Message);
                    return false;
                }
            }

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
                        _constructionShutdownFailureMessage =
                            result.Message + " Disposing the bootstrapper retries construction cleanup.";
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
                    stop = _providerPendingLifecycle.TryStop(TimeSpan.FromMilliseconds(OverlayConfig.RuntimeProviderShutdownTimeoutMs));
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
            _constructionShutdownFailureMessage = string.Empty;
            return true;
        }

        private void SetConstructionShutdownBlocked(string message)
        {
            _constructionShutdownFailureMessage =
                (message ?? "Construction cleanup is blocked.") +
                " Disposing the bootstrapper retries construction cleanup.";
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
