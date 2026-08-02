using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace Malco.Shell.Control
{
    internal static class MalcoControlProtocol
    {
        private const byte ProtocolVersion = 1;
        private const byte QuitCommand = 1;
        private const byte AcceptedStatus = 0;
        private const byte RefusedStatus = 1;
        private const byte ProtocolErrorStatus = 2;
        private const int NonceLength = 32;
        private const int ChallengeFrameLength = 4 + 1 + NonceLength;
        private const int RequestFrameLength = 4 + 1 + 1 + NonceLength + NonceLength;
        private const int ResponseFrameLength = 4 + 1 + 1 + NonceLength;
        private static readonly byte[] Magic = { (byte)'M', (byte)'L', (byte)'C', (byte)'1' };
        private static readonly TimeSpan ClientDeadline = TimeSpan.FromSeconds(3);
        private static readonly int SessionId = GetCurrentSessionId();
        private static readonly string SessionPipeName =
            "Malco.Control.v1.Session-" + SessionId;

        internal static string PipeName => SessionPipeName;
        internal static int InteractiveSessionId => SessionId;

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

        internal static MalcoControlExitCode RequestQuit(string mutexName)
        {
            if (!PrimaryMutexExists(mutexName))
            {
                return MalcoControlExitCode.NotRunning;
            }

            var connected = false;
            using (var deadline = new CancellationTokenSource(ClientDeadline))
            using (var pipe = new NamedPipeClientStream(
                ".",
                PipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous))
            {
                try
                {
                    pipe.ConnectAsync(deadline.Token).GetAwaiter().GetResult();
                    connected = true;

                    using (var server = OpenVerifiedServer(pipe))
                    {
                        var serverStartTime = server.StartTime.ToUniversalTime();
                        var challengeFrame = new byte[ChallengeFrameLength];
                        ReadExactlyAsync(pipe, challengeFrame, deadline.Token).GetAwaiter().GetResult();
                        if (!HasHeader(challengeFrame) || challengeFrame[4] != ProtocolVersion)
                        {
                            return MalcoControlExitCode.ProtocolError;
                        }
                        server.Refresh();
                        if (server.HasExited ||
                            server.StartTime.ToUniversalTime() != serverStartTime)
                        {
                            return MalcoControlExitCode.Refused;
                        }

                        var requestNonce = new byte[NonceLength];
                        RandomNumberGenerator.Fill(requestNonce);
                        var request = new byte[RequestFrameLength];
                        CopyHeader(request);
                        request[4] = ProtocolVersion;
                        request[5] = QuitCommand;
                        Buffer.BlockCopy(challengeFrame, 5, request, 6, NonceLength);
                        Buffer.BlockCopy(requestNonce, 0, request, 6 + NonceLength, NonceLength);
                        WriteFrameAsync(pipe, request, deadline.Token).GetAwaiter().GetResult();

                        var response = new byte[ResponseFrameLength];
                        ReadExactlyAsync(pipe, response, deadline.Token).GetAwaiter().GetResult();
                        if (!HasHeader(response) ||
                            response[4] != ProtocolVersion ||
                            !CryptographicOperations.FixedTimeEquals(
                                response.AsSpan(6, NonceLength),
                                requestNonce))
                        {
                            return MalcoControlExitCode.ProtocolError;
                        }

                        return response[5] switch
                        {
                            AcceptedStatus => MalcoControlExitCode.Success,
                            RefusedStatus => MalcoControlExitCode.Refused,
                            ProtocolErrorStatus => MalcoControlExitCode.ProtocolError,
                            _ => MalcoControlExitCode.ProtocolError
                        };
                    }
                }
                catch (OperationCanceledException)
                {
                    return PrimaryMutexExists(mutexName)
                        ? MalcoControlExitCode.Timeout
                        : MalcoControlExitCode.NotRunning;
                }
                catch (UnauthorizedAccessException)
                {
                    return MalcoControlExitCode.Refused;
                }
                catch (InvalidOperationException)
                {
                    return MalcoControlExitCode.Refused;
                }
                catch (ArgumentException)
                {
                    return MalcoControlExitCode.Refused;
                }
                catch (System.ComponentModel.Win32Exception)
                {
                    return MalcoControlExitCode.Refused;
                }
                catch (IOException)
                {
                    if (!PrimaryMutexExists(mutexName))
                    {
                        return MalcoControlExitCode.NotRunning;
                    }
                    return connected
                        ? MalcoControlExitCode.ProtocolError
                        : MalcoControlExitCode.Timeout;
                }
            }
        }

        internal static byte[] CreateChallengeFrame(byte[] challenge)
        {
            var frame = new byte[ChallengeFrameLength];
            CopyHeader(frame);
            frame[4] = ProtocolVersion;
            Buffer.BlockCopy(challenge, 0, frame, 5, NonceLength);
            return frame;
        }

        internal static bool TryValidateQuitRequest(
            byte[] request,
            byte[] challenge,
            out byte[] requestNonce)
        {
            requestNonce = new byte[NonceLength];
            if (request.Length != RequestFrameLength)
            {
                return false;
            }
            Buffer.BlockCopy(request, 6 + NonceLength, requestNonce, 0, NonceLength);
            return HasHeader(request) &&
                   request[4] == ProtocolVersion &&
                   request[5] == QuitCommand &&
                   CryptographicOperations.FixedTimeEquals(
                       request.AsSpan(6, NonceLength),
                       challenge);
        }

        internal static byte[] CreateResponseFrame(byte status, byte[] requestNonce)
        {
            var frame = new byte[ResponseFrameLength];
            CopyHeader(frame);
            frame[4] = ProtocolVersion;
            frame[5] = status;
            Buffer.BlockCopy(requestNonce, 0, frame, 6, NonceLength);
            return frame;
        }

        internal static byte Accepted => AcceptedStatus;
        internal static byte Refused => RefusedStatus;
        internal static byte InvalidProtocol => ProtocolErrorStatus;
        internal static int RequestBytes => RequestFrameLength;

        internal static async Task ReadExactlyAsync(
            Stream stream,
            byte[] buffer,
            CancellationToken cancellationToken)
        {
            var offset = 0;
            while (offset != buffer.Length)
            {
                var count = await stream.ReadAsync(
                    buffer,
                    offset,
                    buffer.Length - offset,
                    cancellationToken).ConfigureAwait(false);
                if (count == 0)
                {
                    throw new EndOfStreamException("Malco control frame ended early.");
                }
                offset += count;
            }
        }

        internal static async Task WriteFrameAsync(
            Stream stream,
            byte[] frame,
            CancellationToken cancellationToken)
        {
            await stream.WriteAsync(
                frame,
                0,
                frame.Length,
                cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        private static bool PrimaryMutexExists(string mutexName)
        {
            try
            {
                using (Mutex.OpenExisting(mutexName))
                {
                    return true;
                }
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return true;
            }
        }

        private static Process OpenVerifiedServer(NamedPipeClientStream pipe)
        {
            uint serverProcessId;
            uint serverSessionId;
            if (!GetNamedPipeServerProcessId(pipe.SafePipeHandle, out serverProcessId) ||
                !GetNamedPipeServerSessionId(pipe.SafePipeHandle, out serverSessionId) ||
                serverProcessId == 0 ||
                serverProcessId > int.MaxValue ||
                serverSessionId != (uint)InteractiveSessionId)
            {
                throw new UnauthorizedAccessException("Malco control server identity is unavailable.");
            }

            var server = Process.GetProcessById((int)serverProcessId);
            try
            {
                using (var current = Process.GetCurrentProcess())
                {
                    if (server.HasExited ||
                        server.SessionId != InteractiveSessionId ||
                        !SameExecutable(server, current))
                    {
                        throw new UnauthorizedAccessException("Malco control server identity is invalid.");
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

        private static bool HasHeader(byte[] frame)
        {
            return frame.Length >= Magic.Length &&
                   CryptographicOperations.FixedTimeEquals(
                       frame.AsSpan(0, Magic.Length),
                       Magic);
        }

        private static void CopyHeader(byte[] frame)
        {
            Buffer.BlockCopy(Magic, 0, frame, 0, Magic.Length);
        }

        private static int GetCurrentSessionId()
        {
            using (var current = Process.GetCurrentProcess())
            {
                return current.SessionId;
            }
        }
    }
}
