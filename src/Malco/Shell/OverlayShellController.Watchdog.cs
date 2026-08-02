using System;
using System.Threading;
using System.Windows.Threading;
using Malco.Interop;

namespace Malco.Shell
{
    internal sealed partial class OverlayShellController
    {
        private void OnWatchdog(object sender, EventArgs args)
        {
            if (_runtimeHooksDetached || _settings.ResourcesDisposed) return;

            if (Interlocked.Exchange(ref _wakeFallbackPending, 0) != 0 &&
                Volatile.Read(ref _dirtyMask) != 0)
            {
                DrainShellWake();
            }
            if (_runtimeHooksDetached || _settings.ShutdownRequested || _settings.ResourcesDisposed) return;

            var foreground = NativeMethods.GetForegroundWindow();
            var foregroundRoot = foreground != IntPtr.Zero
                ? NativeMethods.GetAncestor(foreground, NativeMethods.GaRoot)
                : IntPtr.Zero;
            if (foregroundRoot != _lastForegroundRoot)
            {
                _lastForegroundRoot = foregroundRoot;
                var trackedProcessId = _targetWindow != null ? _targetWindow.ProcessId : 0;
                if (!_hasTrackedProcess &&
                    _windowTracker.IsPotentialStarCraftWindow(foregroundRoot, trackedProcessId))
                {
                    _windowTracker.Invalidate();
                    _targetWindow = null;
                    _nextTargetAcquisitionTimestamp = 0;
                }
            }
            if (Interlocked.Exchange(ref _foregroundDirty, 0) != 0) _lastForegroundRoot = IntPtr.Zero;

            if (!TrackStarCraftWindow() || _settings.ShutdownRequested) return;
            if (_objectWinEventHook == IntPtr.Zero && _targetWindow != null)
                Interlocked.Exchange(ref _geometryDirty, 1);
            var targetUsable = HasUsableTargetWindow();
            var targetUsabilityChanged = targetUsable != _lastWatchdogTargetUsable;
            _lastWatchdogTargetUsable = targetUsable;
            if (targetUsabilityChanged && targetUsable) Interlocked.Exchange(ref _geometryDirty, 1);
            if (targetUsabilityChanged) _framePump.RequestFrame();
            RefreshRuntimeMode();
            if (targetUsabilityChanged && !targetUsable &&
                (CurrentMode == OverlayRuntimeMode.Editor || CurrentMode == OverlayRuntimeMode.SettingsOnly))
                ApplyRuntimeWindowBounds(CurrentMode);
            ApplyWindowStacking();
        }

        private void OnDisplaySettingsChanged()
        {
            if (_runtimeHooksDetached || _disposed) return;
            _view.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                if (_runtimeHooksDetached || _disposed) return;
                _presentation.InvalidateSpatialSurface();
                Interlocked.Exchange(ref _geometryDirty, 1);
                _framePump.RequestFrame();
            }));
        }
    }
}
