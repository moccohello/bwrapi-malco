using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace Malco.Shell.Tray
{
    internal readonly struct NativePoint
    {
        public NativePoint(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X { get; }
        public int Y { get; }
    }

    internal sealed class NativeTrayIcon : IDisposable
    {
        private const uint CallbackMessage = 0x8001;
        private const uint NimAdd = 0;
        private const uint NimModify = 1;
        private const uint NimDelete = 2;
        private const uint NimSetVersion = 4;
        private const uint NifMessage = 1;
        private const uint NifIcon = 2;
        private const uint NifTip = 4;
        private const uint NotifyIconVersion = 4;
        private const int NinKeySelect = 0x0401;
        private const int WmLButtonDoubleClick = 0x0203;
        private const int WmRButtonUp = 0x0205;
        private const int WmContextMenu = 0x007B;
        private const uint ImageIcon = 1;
        private const uint LoadFromFile = 0x0010;
        private const uint LoadDefaultSize = 0x0040;

        private readonly HwndSource _messageSource;
        private readonly Action _openSettings;
        private readonly Action<NativePoint> _showMenu;
        private readonly uint _taskbarCreatedMessage;
        private readonly bool _ownsIcon;
        private IntPtr _iconHandle;
        private string _tooltip;
        private bool _disposed;

        public NativeTrayIcon(
            string iconPath,
            string tooltip,
            Action openSettings,
            Action<NativePoint> showMenu)
        {
            _openSettings = openSettings ?? throw new ArgumentNullException(nameof(openSettings));
            _showMenu = showMenu ?? throw new ArgumentNullException(nameof(showMenu));
            _tooltip = NormalizeTooltip(tooltip);

            var parameters = new HwndSourceParameters("Malco.TrayMessageWindow")
            {
                Width = 0,
                Height = 0,
                // TaskbarCreated is broadcast only to top-level windows, not HWND_MESSAGE windows.
                WindowStyle = unchecked((int)0x80000000)
            };
            _messageSource = new HwndSource(parameters);
            try
            {
                _messageSource.AddHook(WindowProcedure);
                _taskbarCreatedMessage = RegisterWindowMessage("TaskbarCreated");
                if (_taskbarCreatedMessage == 0)
                {
                    throw new InvalidOperationException("The Explorer restart notification could not be registered.");
                }
                if (!string.IsNullOrWhiteSpace(iconPath))
                {
                    _iconHandle = LoadImage(IntPtr.Zero, iconPath, ImageIcon, 0, 0, LoadFromFile | LoadDefaultSize);
                    _ownsIcon = _iconHandle != IntPtr.Zero;
                }
                if (_iconHandle == IntPtr.Zero)
                {
                    _iconHandle = LoadIcon(IntPtr.Zero, new IntPtr(32512));
                }
                if (_iconHandle == IntPtr.Zero)
                {
                    throw new InvalidOperationException("The tray icon could not be loaded.");
                }
                if (!TryAddIcon())
                {
                    throw new InvalidOperationException("The tray icon could not be registered.");
                }
            }
            catch
            {
                _messageSource.RemoveHook(WindowProcedure);
                _messageSource.Dispose();
                if (_ownsIcon)
                {
                    DestroyIcon(_iconHandle);
                }
                throw;
            }
        }

        public void SetTooltip(string tooltip)
        {
            if (_disposed)
            {
                return;
            }

            _tooltip = NormalizeTooltip(tooltip);
            var data = CreateData(NifTip);
            ShellNotifyIcon(NimModify, ref data);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            var data = CreateData(0);
            ShellNotifyIcon(NimDelete, ref data);
            _messageSource.RemoveHook(WindowProcedure);
            _messageSource.Dispose();
            if (_ownsIcon)
            {
                DestroyIcon(_iconHandle);
            }
            _iconHandle = IntPtr.Zero;
        }

        private bool TryAddIcon()
        {
            var data = CreateData(NifMessage | NifIcon | NifTip);
            if (!ShellNotifyIcon(NimAdd, ref data))
            {
                return false;
            }

            data.uVersion = NotifyIconVersion;
            ShellNotifyIcon(NimSetVersion, ref data);
            return true;
        }

        private IntPtr WindowProcedure(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if ((uint)message == _taskbarCreatedMessage)
            {
                TryAddIcon();
                handled = true;
                return IntPtr.Zero;
            }

            if ((uint)message != CallbackMessage)
            {
                return IntPtr.Zero;
            }

            var notification = unchecked((int)(lParam.ToInt64() & 0xffff));
            switch (notification)
            {
                case WmLButtonDoubleClick:
                case NinKeySelect:
                    _openSettings();
                    handled = true;
                    break;
                case WmRButtonUp:
                    if (GetCursorPos(out var cursor))
                    {
                        SetForegroundWindow(hwnd);
                        _showMenu(new NativePoint(cursor.X, cursor.Y));
                    }
                    handled = true;
                    break;
                case WmContextMenu:
                    if (TryGetContextMenuPoint(wParam, out var menuPoint))
                    {
                        SetForegroundWindow(hwnd);
                        _showMenu(menuPoint);
                    }
                    handled = true;
                    break;
            }

            return IntPtr.Zero;
        }

        private bool TryGetContextMenuPoint(IntPtr messagePoint, out NativePoint point)
        {
            var packed = messagePoint.ToInt64();
            var x = unchecked((short)(packed & 0xffff));
            var y = unchecked((short)((packed >> 16) & 0xffff));
            if (x != -1 || y != -1)
            {
                point = new NativePoint(x, y);
                return true;
            }

            var identifier = new NotifyIconIdentifier
            {
                Size = (uint)Marshal.SizeOf<NotifyIconIdentifier>(),
                Window = _messageSource.Handle,
                Id = 1
            };
            if (ShellNotifyIconGetRect(ref identifier, out var bounds) >= 0)
            {
                point = new NativePoint((bounds.Left + bounds.Right) / 2, bounds.Top);
                return true;
            }
            if (GetCursorPos(out var cursor))
            {
                point = new NativePoint(cursor.X, cursor.Y);
                return true;
            }
            point = default;
            return false;
        }

        private NotifyIconData CreateData(uint flags)
        {
            return new NotifyIconData
            {
                cbSize = (uint)Marshal.SizeOf<NotifyIconData>(),
                hWnd = _messageSource.Handle,
                uID = 1,
                uFlags = flags,
                uCallbackMessage = CallbackMessage,
                hIcon = _iconHandle,
                szTip = _tooltip
            };
        }

        private static string NormalizeTooltip(string tooltip)
        {
            var value = string.IsNullOrWhiteSpace(tooltip) ? "Malco" : tooltip.Trim();
            return value.Length <= 127 ? value : value.Substring(0, 127);
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NotifyIconData
        {
            public uint cbSize;
            public IntPtr hWnd;
            public uint uID;
            public uint uFlags;
            public uint uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
            public uint dwState;
            public uint dwStateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
            public uint uVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
            public uint dwInfoFlags;
            public Guid guidItem;
            public IntPtr hBalloonIcon;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Point
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Rect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NotifyIconIdentifier
        {
            public uint Size;
            public IntPtr Window;
            public uint Id;
            public Guid Guid;
        }

        [DllImport("shell32.dll", EntryPoint = "Shell_NotifyIconW", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ShellNotifyIcon(uint message, ref NotifyIconData data);

        [DllImport("shell32.dll")]
        private static extern int ShellNotifyIconGetRect(ref NotifyIconIdentifier identifier, out Rect iconLocation);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadImage(IntPtr instance, string name, uint type, int width, int height, uint flags);

        [DllImport("user32.dll")]
        private static extern IntPtr LoadIcon(IntPtr instance, IntPtr iconName);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyIcon(IntPtr icon);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern uint RegisterWindowMessage(string message);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out Point point);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr window);
    }
}
