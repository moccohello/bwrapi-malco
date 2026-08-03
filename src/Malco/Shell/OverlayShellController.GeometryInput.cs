using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using Malco.Interop;
using Malco.Overlay;

namespace Malco.Shell
{
    internal sealed partial class OverlayShellController
    {
        private void ApplyRuntimeWindowBounds(OverlayRuntimeMode mode)
        {
            if (!HasUsableTargetWindow())
            {
                _hasAppliedTargetBounds = false;
                var width = mode == OverlayRuntimeMode.Editor ? 1220d : 112d;
                var height = mode == OverlayRuntimeMode.Editor ? 570d : 38d;
                var left = _hasLastKnownWorkArea ? _lastKnownWorkAreaDip.Left + 12d : 12d;
                var top = _hasLastKnownWorkArea ? _lastKnownWorkAreaDip.Top + 12d : 12d;
                if (_hasLastKnownWorkArea)
                {
                    width = Math.Min(width, Math.Max(64d, _lastKnownWorkAreaDip.Width - 24d));
                    height = Math.Min(height, Math.Max(32d, _lastKnownWorkAreaDip.Height - 24d));
                }
                SetOverlayBounds(new Rect(left, top, width, height), false);
                _view.PositionSettingsButtonAtOrigin();
                return;
            }
            var monitorOrDpiChanged = !_hasAppliedTargetBounds ||
                                      _lastAppliedTargetMonitor != _targetWindow.Monitor ||
                                      _lastAppliedTargetDpi != _targetWindow.Dpi;
            if (_hasAppliedTargetBounds && _lastAppliedTargetDeviceBounds == _targetWindow.Bounds &&
                !monitorOrDpiChanged && _lastAppliedBoundsMode == mode) return;
            var rect = ToDipRect(_targetWindow.Bounds);
            _hasAppliedTargetBounds = true;
            _lastAppliedTargetDeviceBounds = _targetWindow.Bounds;
            _lastAppliedTargetDpi = _targetWindow.Dpi;
            _lastAppliedTargetMonitor = _targetWindow.Monitor;
            _lastAppliedBoundsMode = mode;
            if (mode == OverlayRuntimeMode.SettingsOnly)
            {
                SetOverlayBounds(new Rect(rect.Left + 12d, rect.Top + 12d, 112d, 38d), false, monitorOrDpiChanged);
                _view.PositionSettingsButtonAtOrigin();
                return;
            }
            SetOverlayBounds(rect, true, monitorOrDpiChanged);
        }

        private bool HasUsableTargetWindow()
        {
            return _targetWindow != null && _targetWindow.Handle != IntPtr.Zero &&
                   _windowTracker.HasCachedIdentity && NativeMethods.IsWindow(_targetWindow.Handle) &&
                   NativeMethods.IsWindowVisible(_targetWindow.Handle) && !NativeMethods.IsIconic(_targetWindow.Handle);
        }

        private bool IsTrackedTargetMinimized()
        {
            return _targetWindow != null && _targetWindow.Handle != IntPtr.Zero &&
                   NativeMethods.IsWindow(_targetWindow.Handle) &&
                   NativeMethods.IsIconic(_targetWindow.Handle);
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

        private void CaptureLastKnownWorkArea(GameWindowInfo target)
        {
            if (target == null || target.Monitor == IntPtr.Zero) return;
            var monitorInfo = new NativeMethods.MonitorInfo { Size = Marshal.SizeOf<NativeMethods.MonitorInfo>() };
            if (!NativeMethods.GetMonitorInfo(target.Monitor, ref monitorInfo)) return;
            var dpi = target.Dpi > 0 ? target.Dpi : 96u;
            var scale = 96d / dpi;
            var monitorLeft = monitorInfo.Monitor.Left;
            var monitorTop = monitorInfo.Monitor.Top;
            _lastKnownWorkAreaDip = new Rect(
                monitorLeft + (monitorInfo.Work.Left - monitorLeft) * scale,
                monitorTop + (monitorInfo.Work.Top - monitorTop) * scale,
                (monitorInfo.Work.Right - monitorInfo.Work.Left) * scale,
                (monitorInfo.Work.Bottom - monitorInfo.Work.Top) * scale);
            _hasLastKnownWorkArea = true;
        }

        private void SetInputMode(bool interactive)
        {
            if (_handle == IntPtr.Zero) return;
            var exStyle = _appliedExtendedStyle != int.MinValue
                ? _appliedExtendedStyle
                : NativeMethods.GetWindowLong(_handle, NativeMethods.GwlExStyle);
            exStyle |= WsExAppWindow;
            if (interactive) { exStyle &= ~WsExTransparent; exStyle &= ~WsExNoActivate; }
            else { exStyle |= WsExTransparent; exStyle |= WsExNoActivate; }
            if (_appliedExtendedStyle == exStyle) return;
            Marshal.SetLastPInvokeError(0);
            var previousStyle = NativeMethods.SetWindowLong(_handle, NativeMethods.GwlExStyle, exStyle);
            if (previousStyle == 0 && Marshal.GetLastPInvokeError() != 0)
            {
                _settings.SetShellStatus("Overlay input style update failed; open Settings from the tray and restart the overlay.");
                return;
            }
            _appliedExtendedStyle = exStyle;
            _frameChangePending = true;
        }

        private void ApplyWindowStacking()
        {
            if (_handle == IntPtr.Zero) return;
            SetInputMode(CurrentMode == OverlayRuntimeMode.SettingsOnly || _settings.EditorMode);
            if (!HasUsableTargetWindow())
            {
                if (_view.IsOverlayTopmost) _view.IsOverlayTopmost = false;
                var mustHide = IsTrackedTargetMinimized() || CurrentMode == OverlayRuntimeMode.Gameplay;
                _view.SetOverlayPresented(!mustHide);
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
            _view.SetOverlayPresented(true);
            if (_view.IsOverlayTopmost) _view.IsOverlayTopmost = false;
            var flags = NativeMethods.SwpNoActivate | NativeMethods.SwpNoMove | NativeMethods.SwpNoSize;
            var windowAboveGame = NativeMethods.GetWindow(_targetWindow.Handle, NativeMethods.GwHwndPrev);
            var alreadyDirectlyAboveGame = windowAboveGame == _handle;
            if (alreadyDirectlyAboveGame && !_frameChangePending) return;
            if (alreadyDirectlyAboveGame) flags |= NativeMethods.SwpNoZOrder;
            if (_frameChangePending) flags |= SwpFrameChanged;
            var insertAfter = windowAboveGame != IntPtr.Zero && !alreadyDirectlyAboveGame
                ? windowAboveGame : NativeMethods.HwndTop;
            var stackingApplied = NativeMethods.SetWindowPos(_handle, insertAfter, 0, 0, 0, 0, flags);
            if (stackingApplied) _frameChangePending = false;
            else _settings.SetShellStatus("Overlay stacking update failed; retrying.");
        }
    }
}
