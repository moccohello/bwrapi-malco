using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Text;
using Malco.Interop;
using static Malco.Overlay.StarCraftWindowGeometry;

namespace Malco.Overlay
{
    internal sealed partial class StarCraftWindowTracker
    {
        private const long ForegroundBonus = 10_000_000_000L;
        private const long IconicPenalty = 5_000_000_000L;

        private List<GameWindowInfo> _enumCandidates;

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
