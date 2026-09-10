using System;
using System.Threading;
using Malco.Interop;

namespace Malco.Shell.Input
{
    internal readonly struct HotkeyRegistrationResult
    {
        public HotkeyRegistrationResult(bool isRegistered, string message)
        {
            IsRegistered = isRegistered;
            Message = message ?? string.Empty;
        }

        public bool IsRegistered { get; }

        public string Message { get; }
    }

    internal sealed class HotkeyController : IDisposable
    {
        internal const int WmHotKey = 0x0312;
        internal const int ToggleEditorHotKeyId = 0x53C1;
        internal const int CloseEditorHotKeyId = 0x53C2;
        internal const uint F8VirtualKey = 0x77;
        internal const uint EscapeVirtualKey = 0x1B;
        internal const uint CtrlShiftModifiers = 0x0002u | 0x0004u;
        internal const uint ModNoRepeat = 0x4000u;
        internal const string ShortcutDisplay = "Ctrl+Shift+F8";
        internal const string RegistrationFailureMessage =
            ShortcutDisplay + " could not be registered. Open Settings from the tray icon.";

        private readonly Action _toggleEditorIntent;
        private readonly Action _closeEditorIntent;
        private IntPtr _registeredWindow;
        private int _toggleRegistered;
        private int _escapeArmed;
        private int _disposed;
        private HotkeyRegistrationResult _registrationResult;

        public HotkeyController(Action toggleEditorIntent, Action closeEditorIntent)
        {
            _toggleEditorIntent = toggleEditorIntent ??
                                  throw new ArgumentNullException(nameof(toggleEditorIntent));
            _closeEditorIntent = closeEditorIntent ??
                                 throw new ArgumentNullException(nameof(closeEditorIntent));
        }

        public HotkeyRegistrationResult RegistrationResult => _registrationResult;

        public HotkeyRegistrationResult Register(IntPtr windowHandle)
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(nameof(HotkeyController));
            }
            if (windowHandle == IntPtr.Zero)
            {
                throw new ArgumentException("A valid window handle is required.", nameof(windowHandle));
            }
            if (_registeredWindow != IntPtr.Zero)
            {
                if (_registeredWindow != windowHandle)
                {
                    throw new InvalidOperationException("The hotkey is already registered to another window.");
                }

                return _registrationResult;
            }

            _registeredWindow = windowHandle;
            var registered = NativeMethods.RegisterHotKey(
                windowHandle,
                ToggleEditorHotKeyId,
                CtrlShiftModifiers,
                F8VirtualKey);
            if (registered)
            {
                Volatile.Write(ref _toggleRegistered, 1);
            }

            _registrationResult = new HotkeyRegistrationResult(
                registered,
                registered ? string.Empty : RegistrationFailureMessage);
            return _registrationResult;
        }

        public void SetEditorEscapeArmed(bool armed)
        {
            if (IsDisposed || _registeredWindow == IntPtr.Zero) return;
            var currentlyArmed = Volatile.Read(ref _escapeArmed) != 0;
            if (armed == currentlyArmed) return;
            if (armed)
            {
                if (!NativeMethods.RegisterHotKey(
                    _registeredWindow,
                    CloseEditorHotKeyId,
                    ModNoRepeat,
                    EscapeVirtualKey))
                {
                    return;
                }
                Volatile.Write(ref _escapeArmed, 1);
                return;
            }

            Volatile.Write(ref _escapeArmed, 0);
            NativeMethods.UnregisterHotKey(_registeredWindow, CloseEditorHotKeyId);
        }

        public bool TryHandleWindowMessage(
            IntPtr windowHandle,
            int message,
            IntPtr wParam,
            IntPtr lParam)
        {
            if (IsDisposed ||
                _registeredWindow == IntPtr.Zero ||
                windowHandle != _registeredWindow ||
                message != WmHotKey)
            {
                return false;
            }

            var id = wParam.ToInt64();
            if (id == ToggleEditorHotKeyId)
            {
                _toggleEditorIntent();
                return true;
            }
            if (id == CloseEditorHotKeyId)
            {
                if (Volatile.Read(ref _escapeArmed) == 0) return true;
                _closeEditorIntent();
                return true;
            }
            return false;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            if (_registeredWindow != IntPtr.Zero)
            {
                if (Interlocked.Exchange(ref _toggleRegistered, 0) != 0)
                {
                    NativeMethods.UnregisterHotKey(_registeredWindow, ToggleEditorHotKeyId);
                }
                if (Interlocked.Exchange(ref _escapeArmed, 0) != 0)
                {
                    NativeMethods.UnregisterHotKey(_registeredWindow, CloseEditorHotKeyId);
                }
                _registeredWindow = IntPtr.Zero;
            }
        }

        private bool IsDisposed => Volatile.Read(ref _disposed) != 0;
    }
}
