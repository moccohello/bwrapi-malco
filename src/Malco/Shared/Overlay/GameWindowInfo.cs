using System;
using System.Drawing;

namespace Malco.Overlay
{
    internal sealed class GameWindowInfo
    {
        public IntPtr Handle { get; set; }

        public int ProcessId { get; set; }

        public string ProcessName { get; set; }

        public DateTime? ProcessStartedAtUtc { get; set; }

        public string Title { get; set; }

        public Rectangle Bounds { get; set; }

        public uint Dpi { get; set; }

        public IntPtr Monitor { get; set; }

        public string Path { get; set; }
    }
}
