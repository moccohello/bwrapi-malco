using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Malco.Shell.Control
{
    internal static class MalcoControlPeerVerifier
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetNamedPipeServerProcessId(
            SafePipeHandle pipe,
            out uint serverProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetNamedPipeServerSessionId(
            SafePipeHandle pipe,
            out uint serverSessionId);

        internal static int GetCurrentSessionId()
        {
            using (var current = Process.GetCurrentProcess())
            {
                return current.SessionId;
            }
        }

        internal static Process OpenVerifiedServer(
            NamedPipeClientStream pipe,
            int interactiveSessionId)
        {
            uint serverProcessId;
            uint serverSessionId;
            if (!GetNamedPipeServerProcessId(pipe.SafePipeHandle, out serverProcessId) ||
                !GetNamedPipeServerSessionId(pipe.SafePipeHandle, out serverSessionId) ||
                serverProcessId == 0 ||
                serverProcessId > int.MaxValue ||
                serverSessionId != (uint)interactiveSessionId)
            {
                throw new UnauthorizedAccessException(
                    "Malco control server identity is unavailable.");
            }

            var server = Process.GetProcessById((int)serverProcessId);
            try
            {
                using (var current = Process.GetCurrentProcess())
                {
                    if (server.HasExited ||
                        server.SessionId != interactiveSessionId ||
                        !SameExecutable(server, current))
                    {
                        throw new UnauthorizedAccessException(
                            "Malco control server identity is invalid.");
                    }
                }
                return server;
            }
            catch
            {
                server.Dispose();
                throw;
            }
        }

        internal static DateTime GetStartTimeUtc(Process server)
        {
            return server.StartTime.ToUniversalTime();
        }

        internal static bool IsSameProcessInstance(
            Process server,
            DateTime startTimeUtc)
        {
            server.Refresh();
            return !server.HasExited &&
                   server.StartTime.ToUniversalTime() == startTimeUtc;
        }

        private static bool SameExecutable(Process left, Process right)
        {
            var leftPath = left.MainModule?.FileName;
            var rightPath = right.MainModule?.FileName;
            return !string.IsNullOrWhiteSpace(leftPath) &&
                   !string.IsNullOrWhiteSpace(rightPath) &&
                   string.Equals(
                       Path.GetFullPath(leftPath),
                       Path.GetFullPath(rightPath),
                       StringComparison.OrdinalIgnoreCase);
        }
    }
}
