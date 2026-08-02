using System;
using System.Runtime.InteropServices;

namespace Malco.Shell.Tray
{
    internal static class NativeMenuMethods
    {
        private const uint MonitorDefaultToNearest = 2;
        private const uint NoSize = 0x0001;
        private const uint NoZOrder = 0x0004;
        private const uint NoActivate = 0x0010;

        internal readonly struct WorkArea
        {
            public WorkArea(int left, int top, int right, int bottom)
            {
                Left = left;
                Top = top;
                Right = right;
                Bottom = bottom;
            }

            public int Left { get; }
            public int Top { get; }
            public int Right { get; }
            public int Bottom { get; }
        }

        internal static WorkArea GetWorkArea(int x, int y)
        {
            var point = new Point { X = x, Y = y };
            var monitor = MonitorFromPoint(point, MonitorDefaultToNearest);
            var info = new MonitorInfo { Size = (uint)Marshal.SizeOf<MonitorInfo>() };
            if (monitor != IntPtr.Zero && GetMonitorInfo(monitor, ref info))
            {
                return new WorkArea(info.Work.Left, info.Work.Top, info.Work.Right, info.Work.Bottom);
            }
            return new WorkArea(0, 0, 1920, 1080);
        }

        internal static void PositionWindow(IntPtr window, int x, int y)
        {
            SetWindowPos(window, IntPtr.Zero, x, y, 0, 0, NoSize | NoZOrder | NoActivate);
        }

        [DllImport("user32.dll")]
        internal static extern uint GetDpiForWindow(IntPtr window);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPos(
            IntPtr window,
            IntPtr insertAfter,
            int x,
            int y,
            int width,
            int height,
            uint flags);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(Point point, uint flags);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

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

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct MonitorInfo
        {
            public uint Size;
            public Rect Monitor;
            public Rect Work;
            public uint Flags;
        }
    }
}
