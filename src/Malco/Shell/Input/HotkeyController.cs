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
        internal const uint F8VirtualKey = 0x77;
        internal const uint CtrlShiftModifiers = 0x0002u | 0x0004u;
        internal const string ShortcutDisplay = "Ctrl+Shift+F8";
        internal const string RegistrationFailureMessage =
            ShortcutDisplay + " could not be registered. Open Settings from the tray icon.";

        private readonly Action _toggleEditorIntent;
        private IntPtr _registeredWindow;
        private int _registered;
        private int _disposed;
        private HotkeyRegistrationResult _registrationResult;

        public HotkeyController(Action toggleEditorIntent)
        {
            _toggleEditorIntent = toggleEditorIntent ??
                                  throw new ArgumentNullException(nameof(toggleEditorIntent));
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
            if (Volatile.Read(ref _registered) != 0)
            {
                if (_registeredWindow != windowHandle)
                {
                    throw new InvalidOperationException("The hotkey is already registered to another window.");
                }

                return _registrationResult;
            }

            var registered = NativeMethods.RegisterHotKey(
                windowHandle,
                ToggleEditorHotKeyId,
                CtrlShiftModifiers,
                F8VirtualKey);
            if (registered)
            {
                _registeredWindow = windowHandle;
                Volatile.Write(ref _registered, 1);
            }

            _registrationResult = new HotkeyRegistrationResult(
                registered,
                registered ? string.Empty : RegistrationFailureMessage);
            return _registrationResult;
        }

        public bool TryHandleWindowMessage(
            IntPtr windowHandle,
            int message,
            IntPtr wParam,
            IntPtr lParam)
        {
            if (IsDisposed ||
                Volatile.Read(ref _registered) == 0 ||
                windowHandle != _registeredWindow ||
                message != WmHotKey ||
                wParam.ToInt64() != ToggleEditorHotKeyId)
            {
                return false;
            }

            _toggleEditorIntent();
            return true;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            if (Interlocked.Exchange(ref _registered, 0) != 0)
            {
                NativeMethods.UnregisterHotKey(_registeredWindow, ToggleEditorHotKeyId);
                _registeredWindow = IntPtr.Zero;
            }
        }

        private bool IsDisposed => Volatile.Read(ref _disposed) != 0;
    }
}
