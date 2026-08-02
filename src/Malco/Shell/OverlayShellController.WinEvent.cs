using System;
using System.Threading;
using Malco.Interop;

namespace Malco.Shell
{
    internal sealed partial class OverlayShellController
    {
        private void OnWinEvent(IntPtr hook, uint eventType, IntPtr eventWindow, int objectId,
            int childId, uint eventThread, uint eventTime)
        {
            if (_settings.ShutdownRequested || eventWindow == IntPtr.Zero) return;
            var systemEvent = eventType == NativeMethods.EventSystemForeground ||
                              eventType == NativeMethods.EventSystemMinimizeStart ||
                              eventType == NativeMethods.EventSystemMinimizeEnd;
            var objectEvent = eventType == NativeMethods.EventObjectDestroy ||
                              eventType == NativeMethods.EventObjectShow ||
                              eventType == NativeMethods.EventObjectHide ||
                              eventType == NativeMethods.EventObjectLocationChange;
            if ((!systemEvent && !objectEvent) || childId != NativeMethods.ChildidSelf ||
                (objectEvent && objectId != NativeMethods.ObjidWindow)) return;
            int dirty;
            if (eventType == NativeMethods.EventSystemForeground) dirty = ShellDirtyForeground;
            else
            {
                var targetHandle = Interlocked.CompareExchange(ref _eventTargetHandle, IntPtr.Zero, IntPtr.Zero);
                var root = NativeMethods.GetAncestor(eventWindow, NativeMethods.GaRoot);
                uint processId;
                NativeMethods.GetWindowThreadProcessId(eventWindow, out processId);
                if (targetHandle == IntPtr.Zero || processId != (uint)Volatile.Read(ref _eventTargetProcessId) ||
                    (eventWindow != targetHandle && root != targetHandle)) return;
                dirty = eventType == NativeMethods.EventObjectLocationChange
                    ? ShellDirtyGeometry : ShellDirtyValidity | ShellDirtyGeometry;
            }
            Interlocked.Or(ref _dirtyMask, dirty);
            if ((dirty & ShellDirtyGeometry) != 0)
            {
                Interlocked.Exchange(ref _geometryDirty, 1);
                _framePump.RequestFrame();
            }
            if (Interlocked.CompareExchange(ref _wakeQueued, 1, 0) == 0)
            {
                var overlayHandle = Interlocked.CompareExchange(ref _handle, IntPtr.Zero, IntPtr.Zero);
                if (overlayHandle == IntPtr.Zero ||
                    !NativeMethods.PostMessage(overlayHandle, (uint)WmShellWake, IntPtr.Zero, IntPtr.Zero))
                {
                    Interlocked.Exchange(ref _wakeQueued, 0);
                    Interlocked.Exchange(ref _wakeFallbackPending, 1);
                }
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (_settings.ShutdownRequested) return IntPtr.Zero;
            if (_hotkey.TryHandleWindowMessage(hwnd, message, wParam, lParam)) handled = true;
            else if (message == WmShellWake) { DrainShellWake(); handled = true; }
            return IntPtr.Zero;
        }

        private void DrainShellWake()
        {
            while (true)
            {
                var dirty = Interlocked.Exchange(ref _dirtyMask, 0);
                if ((dirty & (ShellDirtyGeometry | ShellDirtyValidity)) != 0) Interlocked.Exchange(ref _geometryDirty, 1);
                if ((dirty & ShellDirtyValidity) != 0) Interlocked.Exchange(ref _validityDirty, 1);
                if ((dirty & ShellDirtyForeground) != 0) Interlocked.Exchange(ref _foregroundDirty, 1);
                Interlocked.Exchange(ref _wakeQueued, 0);
                if (Volatile.Read(ref _dirtyMask) == 0 || Interlocked.CompareExchange(ref _wakeQueued, 1, 0) != 0) break;
            }
            if (Interlocked.Exchange(ref _validityDirty, 0) != 0)
            {
                if (!TrackStarCraftWindow() || _settings.ShutdownRequested) return;
                RefreshRuntimeMode();
                if (CurrentMode == OverlayRuntimeMode.Editor || CurrentMode == OverlayRuntimeMode.SettingsOnly)
                    ApplyRuntimeWindowBounds(CurrentMode);
                ApplyWindowStacking();
            }
        }
    }
}
