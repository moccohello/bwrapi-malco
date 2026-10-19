using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using Malco.Interop;
using Malco.Overlay;
using static Malco.Overlay.StarCraftWindowGeometry;

namespace Malco.Shell
{
    internal sealed partial class OverlayShellController
    {
        private void ApplyOverlayAttachment()
        {
            if (HasUsableTargetWindow())
            {
                ApplyGameClientBounds(CurrentMode);
                PresentOverlay(true);
                return;
            }

            _hasAppliedTargetBounds = false;
            PresentOverlay(false);
        }

        private void ApplyGameClientBounds(OverlayRuntimeMode mode)
        {
            Rectangle bounds;
            if (!TryGetClientBounds(_targetWindow.Handle, out bounds) || !HasUsableClientBounds(bounds))
            {
                return;
            }

            var monitorOrDpiChanged = !_hasAppliedTargetBounds ||
                                      _lastAppliedTargetMonitor != _targetWindow.Monitor ||
                                      _lastAppliedTargetDpi != _targetWindow.Dpi;
            var boundsUnchanged = _hasAppliedTargetBounds &&
                                  _lastAppliedTargetDeviceBounds == bounds &&
                                  !monitorOrDpiChanged;
            var rect = ToDipRect(bounds);
            _hasAppliedTargetBounds = true;
            _lastAppliedTargetDeviceBounds = bounds;
            _lastAppliedTargetDpi = _targetWindow.Dpi;
            _lastAppliedTargetMonitor = _targetWindow.Monitor;
            if (!boundsUnchanged)
            {
                SetOverlayBounds(rect, mode != OverlayRuntimeMode.SettingsOnly, monitorOrDpiChanged);
            }
            if (mode == OverlayRuntimeMode.SettingsOnly)
            {
                _view.PositionSettingsButtonAtOrigin();
            }
        }

        private bool HasUsableTargetWindow()
        {
            if (_targetWindow == null || _targetWindow.Handle == IntPtr.Zero ||
                !_windowTracker.HasCachedIdentity || !NativeMethods.IsWindow(_targetWindow.Handle) ||
                !NativeMethods.IsWindowVisible(_targetWindow.Handle) || NativeMethods.IsIconic(_targetWindow.Handle))
            {
                return false;
            }

            Rectangle bounds;
            return TryGetClientBounds(_targetWindow.Handle, out bounds) && HasUsableClientBounds(bounds);
        }

        private bool IsEditorSurfacePresented()
        {
            return _settings.EditorMode && HasUsableTargetWindow();
        }

        private void SetOverlayBounds(Rect rect, bool clampWidgets, bool force = false)
        {
            if (!force && Math.Abs(_view.OverlayLeft - rect.Left) <= .5d &&
                Math.Abs(_view.OverlayTop - rect.Top) <= .5d &&
                Math.Abs(_view.OverlayWidth - rect.Width) <= .5d &&
                Math.Abs(_view.OverlayHeight - rect.Height) <= .5d) return;
            _view.ApplyShellBounds(new Rect(rect.Left, rect.Top,
                Math.Max(64d, rect.Width), Math.Max(32d, rect.Height)), clampWidgets);
        }

        private Rect ToDipRect(Rectangle deviceBounds)
        {
            var dpi = _targetWindow != null && _targetWindow.Dpi > 0 ? _targetWindow.Dpi : 96u;
            var deviceToDip = 96d / dpi;
            var monitorLeft = 0;
            var monitorTop = 0;
            if (_targetWindow != null && _targetWindow.Monitor != IntPtr.Zero)
            {
                var monitorInfo = new NativeMethods.MonitorInfo { Size = Marshal.SizeOf<NativeMethods.MonitorInfo>() };
                if (NativeMethods.GetMonitorInfo(_targetWindow.Monitor, ref monitorInfo))
                {
                    monitorLeft = monitorInfo.Monitor.Left;
                    monitorTop = monitorInfo.Monitor.Top;
                }
            }
            return new Rect(monitorLeft + (deviceBounds.Left - monitorLeft) * deviceToDip,
                monitorTop + (deviceBounds.Top - monitorTop) * deviceToDip,
                deviceBounds.Width * deviceToDip, deviceBounds.Height * deviceToDip);
        }

        private void ApplyInputStyle()
        {
            if (_handle == IntPtr.Zero) return;
            var editorSurface = IsEditorSurfacePresented();
            var presented = HasUsableTargetWindow();
            var clickThrough = !editorSurface && (!presented || CurrentMode == OverlayRuntimeMode.Gameplay);
            var noActivate = !editorSurface;
            var current = NativeMethods.GetWindowLong(_handle, NativeMethods.GwlExStyle);
            var exStyle = current;
            exStyle &= ~WsExAppWindow;
            exStyle |= WsExToolWindow;
            if (clickThrough) exStyle |= WsExTransparent;
            else exStyle &= ~WsExTransparent;
            if (noActivate) exStyle |= WsExNoActivate;
            else exStyle &= ~WsExNoActivate;
            if (exStyle == current) return;
            Marshal.SetLastPInvokeError(0);
            var previousStyle = NativeMethods.SetWindowLong(_handle, NativeMethods.GwlExStyle, exStyle);
            if (previousStyle == 0 && Marshal.GetLastPInvokeError() != 0)
            {
                _settings.SetShellStatus("Overlay input style update failed; open Settings from the tray and restart the overlay.");
                return;
            }
            _frameChangePending = true;
        }

        private IntPtr GetForegroundRoot()
        {
            var foreground = NativeMethods.GetForegroundWindow();
            if (foreground == IntPtr.Zero) return IntPtr.Zero;
            var root = NativeMethods.GetAncestor(foreground, NativeMethods.GaRoot);
            return root == IntPtr.Zero ? foreground : root;
        }

        private bool IsOverlayForeground()
        {
            return _handle != IntPtr.Zero && GetForegroundRoot() == _handle;
        }

        private bool IsGameForegroundGroup()
        {
            var foregroundRoot = GetForegroundRoot();
            if (foregroundRoot == IntPtr.Zero) return false;
            if (foregroundRoot == _handle) return true;
            if (_targetWindow == null || _targetWindow.Handle == IntPtr.Zero) return false;
            if (foregroundRoot == _targetWindow.Handle) return true;
            uint processId;
            NativeMethods.GetWindowThreadProcessId(foregroundRoot, out processId);
            return processId != 0 && processId == (uint)_targetWindow.ProcessId;
        }

        private void SyncEditorEscapeHotkey()
        {
            var arm = IsEditorSurfacePresented() && IsGameForegroundGroup() && !IsOverlayForeground();
            _hotkey.SetEditorEscapeArmed(arm);
        }

        private void ApplyWindowStacking()
        {
            if (_handle == IntPtr.Zero) return;
            ApplyOverlayAttachment();
            ApplyInputStyle();
            SyncEditorEscapeHotkey();
            if (_view.IsOverlayTopmost) _view.IsOverlayTopmost = false;
            if (!HasUsableTargetWindow())
            {
                if (_frameChangePending)
                {
                    var applied = NativeMethods.SetWindowPos(_handle, NativeMethods.HwndTop, 0, 0, 0, 0,
                        NativeMethods.SwpNoActivate | NativeMethods.SwpNoMove |
                        NativeMethods.SwpNoSize | NativeMethods.SwpNoZOrder | SwpFrameChanged);
                    if (applied) _frameChangePending = false;
                    else _settings.SetShellStatus("Overlay window style refresh failed; retrying.");
                }
                return;
            }
            var flags = NativeMethods.SwpNoActivate | NativeMethods.SwpNoMove | NativeMethods.SwpNoSize;
            if (!IsGameForegroundGroup())
            {
                if (!_frameChangePending) return;
                flags |= NativeMethods.SwpNoZOrder | SwpFrameChanged;
                if (NativeMethods.SetWindowPos(_handle, IntPtr.Zero, 0, 0, 0, 0, flags))
                    _frameChangePending = false;
                else
                    _settings.SetShellStatus("Overlay window style refresh failed; retrying.");
                return;
            }
            var windowAboveGame = NativeMethods.GetWindow(_targetWindow.Handle, NativeMethods.GwHwndPrev);
            var alreadyDirectlyAboveGame = windowAboveGame == _handle;
            if (alreadyDirectlyAboveGame && !_frameChangePending) return;
            if (alreadyDirectlyAboveGame) flags |= NativeMethods.SwpNoZOrder;
            if (_frameChangePending) flags |= SwpFrameChanged;
            var insertAfter = alreadyDirectlyAboveGame || windowAboveGame == IntPtr.Zero
                ? NativeMethods.HwndTop
                : windowAboveGame;
            var stackingApplied = NativeMethods.SetWindowPos(_handle, insertAfter, 0, 0, 0, 0, flags);
            if (stackingApplied) _frameChangePending = false;
            else _settings.SetShellStatus("Overlay stacking update failed; retrying.");
        }

        private void PresentOverlay(bool presented)
        {
            var wasPresented = _view.IsOverlayPresented;
            var hiding = wasPresented && !presented;
            _view.SetOverlayPresented(presented);
            if (!presented)
            {
                if (hiding) ReleaseOverlayForeground();
                return;
            }
            if (_handle == IntPtr.Zero) return;
            if (!wasPresented)
            {
                NativeMethods.ShowWindow(_handle, NativeMethods.SwShowNoActivate);
            }
        }

        private void ReleaseOverlayForeground()
        {
            if (!IsOverlayForeground()) return;
            ActivateTargetIfAvailable();
        }
    }
}
