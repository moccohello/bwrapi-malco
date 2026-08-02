using System;
using System.Diagnostics;
using System.Threading;
using BwrApi.Client;

namespace Malco.Data
{
    internal sealed class TrackedObserverSessionBinding : IDisposable
    {
        private readonly object _gate = new object();
        private readonly ManualResetEventSlim _processAvailable =
            new ManualResetEventSlim(false);
        private BwrApiClient _client;
        private int _processId;
        private DateTime _processStartedAtUtc;
        private bool _hasProcessIdentity;
        private int _openedProcessId;
        private DateTime _openedProcessStartedAtUtc;

        public bool TrySetTrackedProcess(int processId, DateTime processStartedAtUtc)
        {
            lock (_gate)
            {
                if (_openedProcessId != 0 &&
                    (_openedProcessId != processId ||
                     _openedProcessStartedAtUtc != processStartedAtUtc))
                {
                    return false;
                }

                _processId = processId;
                _processStartedAtUtc = processStartedAtUtc;
                _hasProcessIdentity = true;
                _processAvailable.Set();
                return true;
            }
        }

        public void ClearTrackedProcess()
        {
            lock (_gate)
            {
                // An opened observer stays pinned to its exact PID/start-time.
                // A transient HWND loss must not silently retarget that session.
                if (_openedProcessId != 0)
                {
                    return;
                }

                _hasProcessIdentity = false;
                _processId = 0;
                _processStartedAtUtc = default(DateTime);
                _processAvailable.Reset();
            }
        }

        public TrackedProcessIdentity WaitForTrackedProcess(CancellationToken token)
        {
            while (true)
            {
                token.ThrowIfCancellationRequested();
                _processAvailable.Wait(token);
                lock (_gate)
                {
                    if (_hasProcessIdentity)
                    {
                        return new TrackedProcessIdentity(
                            _processId,
                            _processStartedAtUtc);
                    }
                }
            }
        }

        public BwrApiClient OpenBoundClient(
            TrackedProcessIdentity identity,
            string productId,
            CancellationToken token)
        {
            ValidateTrackedProcess(identity);
            var client = BwrApiClient.OpenNeutralObserver(
                new BwrApiNeutralObserverOptions(
                    productId,
                    checked((uint)identity.ProcessId))
                {
                    ExpectedProcessStartedAtUtc = identity.ProcessStartedAtUtc
                },
                token);
            try
            {
                lock (_gate)
                {
                    if (!_hasProcessIdentity ||
                        _processId != identity.ProcessId ||
                        _processStartedAtUtc != identity.ProcessStartedAtUtc)
                    {
                        throw new InvalidOperationException(
                            "Tracked StarCraft identity changed while the observer session was opening.");
                    }

                    _openedProcessId = identity.ProcessId;
                    _openedProcessStartedAtUtc = identity.ProcessStartedAtUtc;
                    Volatile.Write(ref _client, client);
                }

                return client;
            }
            catch
            {
                client.Dispose();
                throw;
            }
        }

        public void SignalWaiters()
        {
            _processAvailable.Set();
        }

        public void CancelClientWait()
        {
            try
            {
                Volatile.Read(ref _client)?.CancelWait();
            }
            catch
            {
            }
        }

        public void DetachClient(BwrApiClient expectedClient)
        {
            Interlocked.CompareExchange(ref _client, null, expectedClient);
        }

        public void Dispose()
        {
            _processAvailable.Dispose();
        }

        private static void ValidateTrackedProcess(TrackedProcessIdentity identity)
        {
            using var process = Process.GetProcessById(identity.ProcessId);
            if (process.HasExited ||
                process.StartTime.ToUniversalTime() != identity.ProcessStartedAtUtc)
            {
                throw new InvalidOperationException(
                    "The retained StarCraft PID/start-time identity is no longer valid.");
            }
        }
    }

    internal readonly struct TrackedProcessIdentity
    {
        public TrackedProcessIdentity(int processId, DateTime processStartedAtUtc)
        {
            ProcessId = processId;
            ProcessStartedAtUtc = processStartedAtUtc;
        }

        public int ProcessId { get; }

        public DateTime ProcessStartedAtUtc { get; }
    }
}
