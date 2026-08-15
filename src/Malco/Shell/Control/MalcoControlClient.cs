using System;
using System.IO;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Threading;

namespace Malco.Shell.Control
{
    internal static class MalcoControlClient
    {
        private static readonly TimeSpan ClientDeadline = TimeSpan.FromSeconds(3);

        internal static MalcoControlExitCode RequestQuit(
            string mutexName,
            string pipeName,
            int interactiveSessionId)
        {
            if (!PrimaryMutexExists(mutexName))
            {
                return MalcoControlExitCode.NotRunning;
            }

            var connected = false;
            using (var deadline = new CancellationTokenSource(ClientDeadline))
            using (var pipe = new NamedPipeClientStream(
                ".",
                pipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous))
            {
                try
                {
                    pipe.ConnectAsync(deadline.Token).GetAwaiter().GetResult();
                    connected = true;

                    using (var server = MalcoControlPeerVerifier.OpenVerifiedServer(
                        pipe,
                        interactiveSessionId))
                    {
                        var serverStartTime =
                            MalcoControlPeerVerifier.GetStartTimeUtc(server);
                        var challengeFrame = new byte[MalcoControlFrameCodec.ChallengeBytes];
                        MalcoControlFrameCodec.ReadExactlyAsync(
                            pipe,
                            challengeFrame,
                            deadline.Token).GetAwaiter().GetResult();
                        if (!MalcoControlFrameCodec.TryReadChallenge(
                            challengeFrame,
                            out var challenge))
                        {
                            return MalcoControlExitCode.ProtocolError;
                        }
                        if (!MalcoControlPeerVerifier.IsSameProcessInstance(
                            server,
                            serverStartTime))
                        {
                            return MalcoControlExitCode.Refused;
                        }

                        var requestNonce = new byte[MalcoControlFrameCodec.NonceBytes];
                        RandomNumberGenerator.Fill(requestNonce);
                        var request = MalcoControlFrameCodec.CreateQuitRequestFrame(
                            challenge,
                            requestNonce);
                        MalcoControlFrameCodec.WriteFrameAsync(
                            pipe,
                            request,
                            deadline.Token).GetAwaiter().GetResult();

                        var response = new byte[MalcoControlFrameCodec.ResponseBytes];
                        MalcoControlFrameCodec.ReadExactlyAsync(
                            pipe,
                            response,
                            deadline.Token).GetAwaiter().GetResult();
                        if (!MalcoControlFrameCodec.TryReadResponseStatus(
                            response,
                            requestNonce,
                            out var status))
                        {
                            return MalcoControlExitCode.ProtocolError;
                        }

                        return status switch
                        {
                            MalcoControlFrameCodec.AcceptedStatus => MalcoControlExitCode.Success,
                            MalcoControlFrameCodec.RefusedStatus => MalcoControlExitCode.Refused,
                            MalcoControlFrameCodec.ProtocolErrorStatus =>
                                MalcoControlExitCode.ProtocolError,
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
                catch (Exception exception) when (
                    exception is UnauthorizedAccessException ||
                    exception is InvalidOperationException ||
                    exception is ArgumentException ||
                    exception is System.ComponentModel.Win32Exception)
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
    }
}
