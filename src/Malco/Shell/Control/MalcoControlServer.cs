using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Malco.Shell.Tray;
using Microsoft.Win32.SafeHandles;

namespace Malco.Shell.Control
{
    internal sealed class MalcoControlServer : IDisposable
    {
        private static readonly TimeSpan RequestDeadline = TimeSpan.FromSeconds(2);
        private readonly Dispatcher _dispatcher;
        private readonly ITrayIntentSink _intentSink;
        private readonly CancellationTokenSource _shutdown = new CancellationTokenSource();
        private readonly object _sync = new object();
        private NamedPipeServerStream _activeServer;
        private Task _serverLoop;
        private int _started;
        private int _disposed;

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetNamedPipeClientSessionId(
            SafePipeHandle pipe,
            out uint clientSessionId);

        public MalcoControlServer(Dispatcher dispatcher, ITrayIntentSink intentSink)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _intentSink = intentSink ?? throw new ArgumentNullException(nameof(intentSink));
        }

        public void Start()
        {
            lock (_sync)
            {
                if (_started != 0)
                {
                    throw new InvalidOperationException("Malco control server is already started.");
                }
                if (_disposed != 0)
                {
                    throw new ObjectDisposedException(nameof(MalcoControlServer));
                }

                var firstServer = CreateServer();
                _activeServer = firstServer;
                _serverLoop = Task.Run(() => RunAsync(firstServer, _shutdown.Token));
                _started = 1;
            }
        }

        public void Dispose()
        {
            NamedPipeServerStream activeServer;
            Task serverLoop;
            lock (_sync)
            {
                if (_disposed != 0)
                {
                    return;
                }
                Volatile.Write(ref _disposed, 1);
                try
                {
                    _shutdown.Cancel();
                }
                catch (Exception error)
                {
                    Debug.WriteLine(error);
                }
                activeServer = _activeServer;
                _activeServer = null;
                serverLoop = _serverLoop;
            }

            try
            {
                activeServer?.Dispose();
            }
            catch (Exception error)
            {
                Debug.WriteLine(error);
            }
            if (serverLoop != null)
            {
                try
                {
                    serverLoop.GetAwaiter().GetResult();
                }
                catch (Exception error) when (
                    error is OperationCanceledException ||
                    error is ObjectDisposedException ||
                    error is IOException)
                {
                }
                catch (Exception error)
                {
                    Debug.WriteLine(error);
                }
            }
            try
            {
                _shutdown.Dispose();
            }
            catch (Exception error)
            {
                Debug.WriteLine(error);
            }
        }

        private async Task RunAsync(
            NamedPipeServerStream firstServer,
            CancellationToken cancellationToken)
        {
            var server = firstServer;
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                        await HandleConnectionAsync(server, cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            break;
                        }
                    }
                    catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (IOException)
                    {
                    }
                    finally
                    {
                        lock (_sync)
                        {
                            if (ReferenceEquals(_activeServer, server))
                            {
                                _activeServer = null;
                            }
                        }
                        server.Dispose();
                    }

                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    try
                    {
                        server = CreateServer();
                        lock (_sync)
                        {
                            if (cancellationToken.IsCancellationRequested ||
                                Volatile.Read(ref _disposed) != 0)
                            {
                                server.Dispose();
                                server = null;
                                break;
                            }
                            _activeServer = server;
                        }
                    }
                    catch (IOException)
                    {
                        server = null;
                        break;
                    }
                }
            }
            finally
            {
                if (server != null)
                {
                    lock (_sync)
                    {
                        if (ReferenceEquals(_activeServer, server))
                        {
                            _activeServer = null;
                        }
                    }
                    server.Dispose();
                }
            }
        }

        private async Task HandleConnectionAsync(
            NamedPipeServerStream server,
            CancellationToken shutdownToken)
        {
            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(shutdownToken))
            {
                deadline.CancelAfter(RequestDeadline);
                var sameSessionClient = IsSameSessionClient(server);
                var challenge = new byte[MalcoControlFrameCodec.NonceBytes];
                RandomNumberGenerator.Fill(challenge);
                await MalcoControlFrameCodec.WriteFrameAsync(
                    server,
                    MalcoControlFrameCodec.CreateChallengeFrame(challenge),
                    deadline.Token).ConfigureAwait(false);

                var request = new byte[MalcoControlFrameCodec.RequestBytes];
                await MalcoControlFrameCodec.ReadExactlyAsync(
                    server,
                    request,
                    deadline.Token).ConfigureAwait(false);

                byte[] requestNonce;
                var valid = MalcoControlFrameCodec.TryValidateQuitRequest(
                    request,
                    challenge,
                    out requestNonce);
                if (!valid ||
                    !sameSessionClient ||
                    Volatile.Read(ref _disposed) != 0 ||
                    _dispatcher.HasShutdownStarted ||
                    _dispatcher.HasShutdownFinished)
                {
                    var refusedStatus = valid
                        ? MalcoControlFrameCodec.Refused
                        : MalcoControlFrameCodec.InvalidProtocol;
                    await MalcoControlFrameCodec.WriteFrameAsync(
                        server,
                        MalcoControlFrameCodec.CreateResponseFrame(refusedStatus, requestNonce),
                        deadline.Token).ConfigureAwait(false);
                    return;
                }

                var started = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                _ = _dispatcher.BeginInvoke(new Action(() =>
                {
                    started.TrySetResult(true);
                }));
                await started.Task.WaitAsync(deadline.Token).ConfigureAwait(false);
                await MalcoControlFrameCodec.WriteFrameAsync(
                    server,
                    MalcoControlFrameCodec.CreateResponseFrame(
                        MalcoControlFrameCodec.Accepted,
                        requestNonce),
                    deadline.Token).ConfigureAwait(false);
                _ = _dispatcher.BeginInvoke(new Action(_intentSink.RequestQuit));
            }
        }

        private static NamedPipeServerStream CreateServer()
        {
            return new NamedPipeServerStream(
                MalcoControlProtocol.PipeName,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly,
                0,
                0);
        }

        private static bool IsSameSessionClient(NamedPipeServerStream server)
        {
            uint clientSessionId;
            return GetNamedPipeClientSessionId(
                       server.SafePipeHandle,
                       out clientSessionId) &&
                   clientSessionId == (uint)MalcoControlProtocol.InteractiveSessionId;
        }
    }
}
