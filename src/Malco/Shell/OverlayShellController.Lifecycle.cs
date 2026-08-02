using System;
using System.Threading;
using System.Windows.Interop;
using Malco.Interop;

namespace Malco.Shell
{
    internal sealed partial class OverlayShellController
    {
        public void Initialize(IntPtr handle)
        {
            _handle = handle;
            _source = HwndSource.FromHwnd(handle);
            if (_source != null) _source.AddHook(WndProc);

            _systemWinEventHook = NativeMethods.SetWinEventHook(
                NativeMethods.EventSystemForeground,
                NativeMethods.EventSystemMinimizeEnd,
                IntPtr.Zero,
                _winEventCallback,
                0,
                0,
                NativeMethods.WineventOutOfContext | NativeMethods.WineventSkipOwnProcess);
            _objectWinEventHook = NativeMethods.SetWinEventHook(
                NativeMethods.EventObjectDestroy,
                NativeMethods.EventObjectLocationChange,
                IntPtr.Zero,
                _winEventCallback,
                0,
                0,
                NativeMethods.WineventOutOfContext | NativeMethods.WineventSkipOwnProcess);

            if (_systemWinEventHook == IntPtr.Zero || _objectWinEventHook == IntPtr.Zero)
            {
                var message = _systemWinEventHook == IntPtr.Zero && _objectWinEventHook == IntPtr.Zero
                    ? "Window event hooks are unavailable; using the low-rate safety tracker."
                    : _systemWinEventHook == IntPtr.Zero
                        ? "Foreground event hook is unavailable; using the safety watchdog."
                        : "Window movement event hook is unavailable; using low-rate geometry tracking.";
                _settings.SetShellStatus(message);
                _settings.SetShellHelpText(message);
                _settings.ReportWindowEventFallback();
            }

            var registration = _hotkey.Register(handle);
            if (!registration.IsRegistered)
            {
                _settings.SetShellStatus(registration.Message);
                _settings.SetShellHelpText(registration.Message);
                _settings.ReportHotkeyUnavailable();
            }
            SetInputMode(_settings.EditorMode || CurrentMode == OverlayRuntimeMode.SettingsOnly);
            Interlocked.Exchange(ref _geometryDirty, 1);
        }

        public void PrepareForRuntimeShutdown()
        {
            if (_runtimeHooksDetached) return;
            _runtimeHooksDetached = true;
            _watchdog.Stop();
            if (_systemWinEventHook != IntPtr.Zero)
            {
                NativeMethods.UnhookWinEvent(_systemWinEventHook);
                _systemWinEventHook = IntPtr.Zero;
            }
            if (_objectWinEventHook != IntPtr.Zero)
            {
                NativeMethods.UnhookWinEvent(_objectWinEventHook);
                _objectWinEventHook = IntPtr.Zero;
            }
            if (_source != null)
            {
                _source.RemoveHook(WndProc);
                _source = null;
            }
            _windowTracker.Invalidate();
            _trackedGameProcessSink.ClearTrackedGameProcess();
            ReleaseTrackedProcessLifetime();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            PrepareForRuntimeShutdown();
            _watchdog.Tick -= OnWatchdog;
            _displaySettings.Dispose();
            Interlocked.Exchange(ref _handle, IntPtr.Zero);
            _windowTracker.Dispose();
        }
    }
}
