using System;
using System.Threading;
using Malco.Integration.Telemetry;
using Malco.Presentation.Scheduling;
using Malco.Settings.Persistence;
using Malco.Shell;
using Malco.Shell.Control;
using Malco.Shell.Input;
using Malco.Shell.Tray;

namespace Malco.Bootstrap
{
    // Owns normal runtime resource disposal. Shutdown admission remains with
    // OverlayShutdownController; this class only performs the final cleanup.
    internal sealed class OverlayApplicationSession : IDisposable
    {
        private readonly CompositionFramePump _framePump;
        private readonly OverlayHudMetrics _hudMetrics;
        private readonly SettingsPersistenceSession _settingsPersistence;
        private readonly MalcoTelemetryIntegration _telemetry;
        private readonly IDisposable _requiredUpdateMonitor;
        private readonly HotkeyController _hotkeyController;
        private readonly OverlayShellController _shellController;
        private readonly MalcoControlServer _controlServer;
        private readonly TrayController _trayController;
        private int _disposed;

        public OverlayApplicationSession(
            CompositionFramePump framePump,
            OverlayHudMetrics hudMetrics,
            SettingsPersistenceSession settingsPersistence,
            MalcoTelemetryIntegration telemetry,
            IDisposable requiredUpdateMonitor,
            HotkeyController hotkeyController,
            OverlayShellController shellController,
            MalcoControlServer controlServer,
            TrayController trayController)
        {
            _framePump = framePump ?? throw new ArgumentNullException(nameof(framePump));
            _hudMetrics = hudMetrics ?? throw new ArgumentNullException(nameof(hudMetrics));
            _settingsPersistence = settingsPersistence ?? throw new ArgumentNullException(nameof(settingsPersistence));
            _telemetry = telemetry;
            _requiredUpdateMonitor = requiredUpdateMonitor;
            _hotkeyController = hotkeyController ?? throw new ArgumentNullException(nameof(hotkeyController));
            _shellController = shellController ?? throw new ArgumentNullException(nameof(shellController));
            _controlServer = controlServer ?? throw new ArgumentNullException(nameof(controlServer));
            _trayController = trayController ?? throw new ArgumentNullException(nameof(trayController));
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            Run(() => _framePump.Dispose());
            Run(() => _hudMetrics.Dispose());
            Run(() => _settingsPersistence.Dispose());
            Run(() => _telemetry?.Dispose());
            Run(() => _requiredUpdateMonitor?.Dispose());
            Run(() => _hotkeyController.Dispose());
            Run(() => _shellController.Dispose());
            Run(() => _controlServer.Dispose());
            Run(() => _trayController.Dispose());
        }

        private static void Run(Action action)
        {
            try
            {
                action();
            }
            catch
            {
                // Best-effort cleanup preserves the existing diagnostics path.
            }
        }
    }
}
