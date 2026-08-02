using System.Windows.Threading;
using Malco.Application.Projection;
using Malco.Application.Overlay;
using Malco.Configuration;
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
using Malco.Integration.Telemetry;

namespace Malco.Bootstrap
{
    internal sealed class OverlayComposition
    {
        public OverlayApplicationSession ApplicationSession { get; init; }
        public GameCoordinator Coordinator { get; init; }
        public OverlayApplicationController ApplicationController { get; init; }
        public ProjectionPresentationAdapter ProjectionPresentation { get; init; }
        public LayoutLoadResult LayoutLoadResult { get; init; }
        public SettingsController SettingsController { get; init; }
        public SettingsPersistenceSession SettingsPersistence { get; init; }
        public MalcoTelemetryIntegration Telemetry { get; init; }
        public IconLocator Icons { get; init; }
        public HudTileFactory HudTileFactory { get; init; }
        public WorkersPresenter WorkersPresenter { get; init; }
        public UnitsPresenter UnitsPresenter { get; init; }
        public BuildingsPresenter BuildingsPresenter { get; init; }
        public UpgradesPresenter UpgradesPresenter { get; init; }
        public OverlayHudMetrics HudMetrics { get; init; }
        public HudVisualTree HudVisualTree { get; init; }
        public SpatialPresenter SpatialPresenter { get; init; }
        public OverlayScenePresenter ScenePresenter { get; init; }
        public OverlaySceneViewController SceneViewController { get; init; }
        public DispatcherPresentationScheduler PresentationScheduler { get; init; }
        public DispatcherTimer PresentationClock { get; init; }
        public CompositionFramePump FramePump { get; init; }
        public ProjectionCommitSubscription ProjectionCommitSubscription { get; init; }
        public OverlayShutdownController ShutdownController { get; init; }
        public HotkeyController HotkeyController { get; init; }
        public TrayController TrayController { get; init; }
        public MalcoControlServer ControlServer { get; init; }
        public OverlayShellController ShellController { get; init; }
    }
}
