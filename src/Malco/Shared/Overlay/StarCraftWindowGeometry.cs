using System;
using System.Drawing;
using Malco.Interop;

namespace Malco.Overlay
{
    internal static class StarCraftWindowGeometry
    {
        private const int MinimumClientDimension = 100;

        public static uint GetWindowDpi(IntPtr hWnd)
        {
            var dpi = NativeMethods.GetDpiForWindow(hWnd);
            return dpi == 0 ? 96u : dpi;
        }

        public static bool HasUsableClientBounds(Rectangle bounds)
        {
            return bounds.Width > MinimumClientDimension &&
                   bounds.Height > MinimumClientDimension;
        }

        public static bool TryGetClientBounds(IntPtr hWnd, out Rectangle bounds)
        {
            bounds = Rectangle.Empty;
            NativeMethods.Rect client;
            if (!NativeMethods.GetClientRect(hWnd, out client))
            {
                return false;
            }

            var topLeft = new NativeMethods.Point { X = client.Left, Y = client.Top };
            if (!NativeMethods.ClientToScreen(hWnd, ref topLeft))
            {
                return false;
            }

            bounds = new Rectangle(
                topLeft.X,
                topLeft.Y,
                Math.Max(0, client.Right - client.Left),
                Math.Max(0, client.Bottom - client.Top));
            return true;
        }
    }
}
