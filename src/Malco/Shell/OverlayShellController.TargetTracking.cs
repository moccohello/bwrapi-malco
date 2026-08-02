using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using Malco.Interop;
using Malco.Overlay;

namespace Malco.Shell
{
    internal sealed partial class OverlayShellController
    {
        private bool TrackStarCraftWindow()
        {
            if (_runtimeHooksDetached) return false;
            if (HasTrackedProcessExited())
            {
                return RequestShutdownForTrackedProcessExit();
            }
            if (_targetWindow != null &&
                (_targetWindow.Handle == IntPtr.Zero || !NativeMethods.IsWindow(_targetWindow.Handle)))
            {
                _windowTracker.Invalidate();
                ResetTrackedWindow("StarCraft closed");
            }
            GameWindowInfo target;
            if (_windowTracker.TryGetCachedWindow(out target))
            {
                return SetTrackedTarget(target);
            }
            if (_targetWindow != null)
            {
                if (_windowTracker.HasCachedIdentity) return true;
                ResetTrackedWindow("StarCraft closed");
            }
            var now = Stopwatch.GetTimestamp();
            if (now < _nextTargetAcquisitionTimestamp)
            {
                _settings.SetShellStatus("StarCraft window not found");
                return true;
            }
            _nextTargetAcquisitionTimestamp = now + Stopwatch.Frequency;
            if (!_windowTracker.TryAcquireWindow(out target))
            {
                _settings.SetShellStatus("StarCraft window not found");
                return true;
            }
            return SetTrackedTarget(target);
        }

        private bool SetTrackedTarget(GameWindowInfo target)
        {
            if (_hasTrackedProcess &&
                (_lastTrackedProcessId != target.ProcessId ||
                 !string.Equals(_lastTrackedProcessName, target.ProcessName, StringComparison.OrdinalIgnoreCase) ||
                 _lastTrackedProcessStartedAtUtc != target.ProcessStartedAtUtc))
            {
                return false;
            }
            if (!TryRetainTrackedProcessLifetime(target)) return false;
            var identityChanged = _targetWindow == null || _targetWindow.Handle != target.Handle ||
                                  _targetWindow.ProcessId != target.ProcessId ||
                                  _targetWindow.ProcessStartedAtUtc != target.ProcessStartedAtUtc;
            var dimensionsChanged = _targetWindow == null ||
                                    _targetWindow.Bounds.Width != target.Bounds.Width ||
                                    _targetWindow.Bounds.Height != target.Bounds.Height;
            _targetWindow = target;
            CaptureLastKnownWorkArea(target);
            if (!_hasTrackedProcess)
            {
                _hasTrackedProcess = true;
                _lastTrackedProcessId = target.ProcessId;
                _lastTrackedProcessName = target.ProcessName;
                _lastTrackedProcessStartedAtUtc = target.ProcessStartedAtUtc;
                _windowTracker.PinProcessIdentity(target.ProcessId, target.ProcessStartedAtUtc.Value);
                _trackedGameProcessSink.SetTrackedGameProcess(
                    target.ProcessId,
                    target.ProcessStartedAtUtc.Value);
            }
            Interlocked.Exchange(ref _eventTargetHandle, target.Handle);
            Volatile.Write(ref _eventTargetProcessId, target.ProcessId);
            if (identityChanged) Interlocked.Exchange(ref _geometryDirty, 1);
            if (dimensionsChanged && string.IsNullOrEmpty(_hotkey.RegistrationResult.Message))
                _settings.SetShellStatus(string.Format(CultureInfo.InvariantCulture,
                    "Tracking {0} x {1}", target.Bounds.Width, target.Bounds.Height));
            return true;
        }

        private bool TryRetainTrackedProcessLifetime(GameWindowInfo target)
        {
            if (target == null || target.ProcessId <= 0 || !target.ProcessStartedAtUtc.HasValue)
                return false;

            if (_trackedProcessLifetime != null &&
                _trackedProcessLifetimeId == target.ProcessId &&
                _trackedProcessLifetimeStartedAtUtc == target.ProcessStartedAtUtc)
                return HasTrackedProcessExited()
                    ? RequestShutdownForTrackedProcessExit()
                    : true;

            // Once a StarCraft process owns this Malco session, no later window
            // candidate may replace its lifetime handle.
            if (_trackedProcessLifetime != null)
                return false;

            Process candidate = null;
            try
            {
                candidate = Process.GetProcessById(target.ProcessId);
                var startedAtUtc = candidate.StartTime.ToUniversalTime();
                if (candidate.HasExited || startedAtUtc != target.ProcessStartedAtUtc.Value)
                {
                    candidate.Dispose();
                    return false;
                }

                _trackedProcessLifetime = candidate;
                _trackedProcessLifetimeId = target.ProcessId;
                _trackedProcessLifetimeStartedAtUtc = startedAtUtc;
                return true;
            }
            catch
            {
                if (candidate != null) candidate.Dispose();
                return false;
            }
        }

        private bool HasTrackedProcessExited()
        {
            if (_trackedProcessLifetime == null) return false;
            try
            {
                return _trackedProcessLifetime.HasExited;
            }
            catch
            {
                // Process inspection uncertainty must not turn a transient Windows
                // access failure into an application shutdown.
                return false;
            }
        }

        private bool RequestShutdownForTrackedProcessExit()
        {
            if (_trackedProcessExitShutdownRequested) return false;
            _trackedProcessExitShutdownRequested = true;
            _settings.RequestApplicationShutdown();
            return false;
        }

        private void ReleaseTrackedProcessLifetime()
        {
            var process = _trackedProcessLifetime;
            _trackedProcessLifetime = null;
            _trackedProcessLifetimeId = 0;
            _trackedProcessLifetimeStartedAtUtc = null;
            if (process != null) process.Dispose();
        }

        private void ResetTrackedWindow(string message)
        {
            _targetWindow = null;
            Interlocked.Exchange(ref _eventTargetHandle, IntPtr.Zero);
            Volatile.Write(ref _eventTargetProcessId, 0);
            _nextTargetAcquisitionTimestamp = 0;
            _presentation.ResetForMissingTarget(message);
        }
    }
}
