using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Text;
using Malco.Configuration;
using Malco.Interop;

namespace Malco.Overlay
{
    internal sealed class StarCraftWindowTracker : IDisposable
    {
        private const long ForegroundBonus = 10_000_000_000L;
        private const long IconicPenalty = 5_000_000_000L;
        private const int MinimumClientDimension = 100;

        private readonly OverlayConfig _config;
        private GameWindowInfo _lastResolved;
        private Process _trackedProcess;
        private List<GameWindowInfo> _enumCandidates;
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

        public bool TryAcquireWindow(out GameWindowInfo window)
        {
            _enumCandidates = new List<GameWindowInfo>();
            NativeMethods.EnumWindows(HandleWindow, IntPtr.Zero);
            var candidates = _enumCandidates;
            _enumCandidates = null;

            if (candidates.Count == 0)
            {
                Invalidate();
                window = null;
                return false;
            }

            var fg = NativeMethods.GetForegroundWindow();
            var fgRoot = fg != IntPtr.Zero ? NativeMethods.GetAncestor(fg, NativeMethods.GaRoot) : IntPtr.Zero;

            GameWindowInfo best = null;
            long bestScore = long.MinValue;

            foreach (var c in candidates)
            {
                long score = (long)Math.Max(1, c.Bounds.Width) * Math.Max(1, c.Bounds.Height);
                var root = NativeMethods.GetAncestor(c.Handle, NativeMethods.GaRoot);

                if (fgRoot != IntPtr.Zero && root == fgRoot)
                {
                    score += ForegroundBonus;
                }

                if (NativeMethods.IsIconic(c.Handle))
                {
                    score -= IconicPenalty;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    best = c;
                }
            }

            if (best != null)
            {
                _lastResolved = CopyInfo(best);
                if (!TryRetainTrackedProcess(_lastResolved))
                {
                    _lastResolved = null;
                    best = null;
                }
            }

            window = best;
            return window != null;
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

        public bool IsPotentialStarCraftWindow(IntPtr rootWindow, int excludedProcessId)
        {
            if (rootWindow == IntPtr.Zero || !NativeMethods.IsWindow(rootWindow))
            {
                return false;
            }

            uint processId;
            NativeMethods.GetWindowThreadProcessId(rootWindow, out processId);
            if (processId == 0 || processId == excludedProcessId)
            {
                return false;
            }

            try
            {
                using (var process = Process.GetProcessById((int)processId))
                {
                    return IsStarCraftProcess(process.ProcessName, SafeGetPath(process));
                }
            }
            catch
            {
                return false;
            }
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

        private bool HandleWindow(IntPtr hWnd, IntPtr lParam)
        {
            if (!NativeMethods.IsWindowVisible(hWnd))
            {
                return true;
            }

            uint processId;
            NativeMethods.GetWindowThreadProcessId(hWnd, out processId);

            int processIdInt;
            string processName;
            string processPath;
            DateTime? processStartedAtUtc;
            try
            {
                using (var process = Process.GetProcessById((int)processId))
                {
                    processIdInt = process.Id;
                    processName = process.ProcessName;
                    processPath = SafeGetPath(process);
                    processStartedAtUtc = SafeGetStartTimeUtc(process);
                    if (!IsStarCraftProcess(processName, processPath))
                    {
                        return true;
                    }
                    if (_requiredProcessId != 0 &&
                        (processIdInt != _requiredProcessId ||
                         processStartedAtUtc != _requiredProcessStartedAtUtc))
                    {
                        return true;
                    }
                }
            }
            catch
            {
                return true;
            }

            var title = GetTitle(hWnd);
            if (!string.IsNullOrEmpty(_config.TargetWindowTitle) &&
                !string.IsNullOrEmpty(title) &&
                title.IndexOf(_config.TargetWindowTitle, StringComparison.OrdinalIgnoreCase) < 0)
            {
                return true;
            }

            var iconic = NativeMethods.IsIconic(hWnd);
            var bounds = Rectangle.Empty;
            if (!iconic &&
                (!TryGetClientBounds(hWnd, out bounds) || !HasUsableClientBounds(bounds)))
            {
                return true;
            }

            _enumCandidates.Add(
                new GameWindowInfo
                {
                    Handle = hWnd,
                    ProcessId = processIdInt,
                    ProcessName = processName,
                    ProcessStartedAtUtc = processStartedAtUtc,
                    Title = title,
                    Bounds = bounds,
                    Dpi = GetWindowDpi(hWnd),
                    Monitor = NativeMethods.MonitorFromWindow(hWnd, NativeMethods.MonitorDefaultToNearest),
                    Path = processPath
                });

            return true;
        }

        private static uint GetWindowDpi(IntPtr hWnd)
        {
            var dpi = NativeMethods.GetDpiForWindow(hWnd);
            return dpi == 0 ? 96u : dpi;
        }

        private static bool HasUsableClientBounds(Rectangle bounds)
        {
            return bounds.Width > MinimumClientDimension &&
                   bounds.Height > MinimumClientDimension;
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

        private static bool TryGetClientBounds(IntPtr hWnd, out Rectangle bounds)
        {
            bounds = Rectangle.Empty;
            NativeMethods.Rect client;
            if (!NativeMethods.GetClientRect(hWnd, out client))
            {
                return false;
            }

            var topLeft = new NativeMethods.Point { X = client.Left, Y = client.Top };
            if (!NativeMethods.ClientToScreen(hWnd, ref topLeft))
            {
                return false;
            }

            bounds = new Rectangle(
                topLeft.X,
                topLeft.Y,
                Math.Max(0, client.Right - client.Left),
                Math.Max(0, client.Bottom - client.Top));
            return true;
        }

        private bool IsStarCraftProcess(string processName, string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            var imageName = System.IO.Path.GetFileNameWithoutExtension(path);
            if (!string.Equals(imageName, processName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return !string.IsNullOrEmpty(_config.TargetProcessName) &&
                   string.Equals(processName, _config.TargetProcessName, StringComparison.OrdinalIgnoreCase);
        }

        private static string GetTitle(IntPtr hWnd)
        {
            var builder = new StringBuilder(512);
            NativeMethods.GetWindowText(hWnd, builder, builder.Capacity);
            return builder.ToString();
        }

        private static string SafeGetPath(Process process)
        {
            try
            {
                return process.MainModule.FileName ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static DateTime? SafeGetStartTimeUtc(Process process)
        {
            try
            {
                return process.StartTime.ToUniversalTime();
            }
            catch
            {
                return null;
            }
        }
    }
}
