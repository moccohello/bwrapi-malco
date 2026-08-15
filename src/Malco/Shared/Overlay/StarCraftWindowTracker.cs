using System;
using System.Diagnostics;
using System.Drawing;
using Malco.Configuration;
using Malco.Interop;
using static Malco.Overlay.StarCraftWindowGeometry;

namespace Malco.Overlay
{
    internal sealed partial class StarCraftWindowTracker : IDisposable
    {
        private readonly OverlayConfig _config;
        private GameWindowInfo _lastResolved;
        private Process _trackedProcess;
        private int _requiredProcessId;
        private DateTime? _requiredProcessStartedAtUtc;

        public StarCraftWindowTracker(OverlayConfig config)
        {
            _config = config;
        }

        public bool HasCachedIdentity
        {
            get { return _lastResolved != null && _trackedProcess != null; }
        }

        public bool TryGetCachedWindow(out GameWindowInfo window)
        {
            return TryRefreshCachedWindow(false, out window);
        }

        public bool TryRefreshGeometry(out GameWindowInfo window)
        {
            return TryRefreshCachedWindow(true, out window);
        }

        public void PinProcessIdentity(int processId, DateTime processStartedAtUtc)
        {
            _requiredProcessId = processId;
            _requiredProcessStartedAtUtc = processStartedAtUtc.Kind == DateTimeKind.Utc
                ? processStartedAtUtc
                : processStartedAtUtc.ToUniversalTime();
        }

        public void Invalidate()
        {
            _lastResolved = null;
            if (_trackedProcess != null)
            {
                _trackedProcess.Dispose();
                _trackedProcess = null;
            }
        }

        public void Dispose()
        {
            Invalidate();
            _requiredProcessId = 0;
            _requiredProcessStartedAtUtc = null;
        }

        public bool IsForeground(GameWindowInfo window)
        {
            if (window == null)
            {
                return false;
            }

            var foregroundWindow = NativeMethods.GetForegroundWindow();
            if (foregroundWindow == IntPtr.Zero)
            {
                return false;
            }

            if (foregroundWindow == window.Handle)
            {
                return true;
            }

            uint foregroundProcessId;
            NativeMethods.GetWindowThreadProcessId(foregroundWindow, out foregroundProcessId);
            return foregroundProcessId == window.ProcessId;
        }

        private bool TryRefreshCachedWindow(bool refreshGeometry, out GameWindowInfo window)
        {
            window = null;
            if (_lastResolved == null ||
                !NativeMethods.IsWindow(_lastResolved.Handle))
            {
                Invalidate();
                return false;
            }

            try
            {
                if (_trackedProcess == null || _trackedProcess.HasExited)
                {
                    Invalidate();
                    return false;
                }
            }
            catch
            {
                Invalidate();
                return false;
            }

            if (!NativeMethods.IsWindowVisible(_lastResolved.Handle))
            {
                // A process may keep an old hidden HWND alive while creating a
                // replacement. Drop only the window cache; the pinned PID/start
                // identity remains and constrains the next enumeration.
                Invalidate();
                return false;
            }

            if (NativeMethods.IsIconic(_lastResolved.Handle))
            {
                return false;
            }

            uint processId;
            NativeMethods.GetWindowThreadProcessId(_lastResolved.Handle, out processId);
            if (processId != _lastResolved.ProcessId)
            {
                Invalidate();
                return false;
            }

            if (!refreshGeometry && HasUsableClientBounds(_lastResolved.Bounds))
            {
                window = CopyInfo(_lastResolved);
                return true;
            }

            Rectangle bounds;
            if (!TryGetClientBounds(_lastResolved.Handle, out bounds))
            {
                return false;
            }

            if (!HasUsableClientBounds(bounds))
            {
                return false;
            }

            _lastResolved.Bounds = bounds;
            _lastResolved.Dpi = GetWindowDpi(_lastResolved.Handle);
            _lastResolved.Monitor = NativeMethods.MonitorFromWindow(
                _lastResolved.Handle,
                NativeMethods.MonitorDefaultToNearest);
            window = CopyInfo(_lastResolved);
            return true;
        }

        private static GameWindowInfo CopyInfo(GameWindowInfo source)
        {
            return new GameWindowInfo
            {
                Handle = source.Handle,
                ProcessId = source.ProcessId,
                ProcessName = source.ProcessName,
                ProcessStartedAtUtc = source.ProcessStartedAtUtc,
                Title = source.Title,
                Bounds = source.Bounds,
                Dpi = source.Dpi,
                Monitor = source.Monitor,
                Path = source.Path
            };
        }

        private bool TryRetainTrackedProcess(GameWindowInfo window)
        {
            if (_trackedProcess != null)
            {
                _trackedProcess.Dispose();
                _trackedProcess = null;
            }

            try
            {
                var process = Process.GetProcessById(window.ProcessId);
                var startedAtUtc = SafeGetStartTimeUtc(process);
                if (process.HasExited ||
                    !window.ProcessStartedAtUtc.HasValue ||
                    !startedAtUtc.HasValue ||
                    window.ProcessStartedAtUtc.Value != startedAtUtc.Value)
                {
                    process.Dispose();
                    return false;
                }

                _trackedProcess = process;
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
