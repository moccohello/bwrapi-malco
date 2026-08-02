using System;
using System.Threading;
using System.Threading.Tasks;

namespace Malco.Data
{
    internal sealed partial class BwrApiEmbeddedRuntimeProvider
    {
        public void Start()
        {
            lock (_lifecycleGate)
            {
                var state = (ProviderLifecycleState)_lifecycleState;
                if (state == ProviderLifecycleState.Running) return;
                if (state != ProviderLifecycleState.Created)
                    throw new InvalidOperationException("The embedded observer cannot be restarted after shutdown begins.");

                SetLifecycle(ProviderLifecycleState.Starting, "Starting embedded BWRAPI observer");
                _supervisor = Task.Run(() => RunSupervisor(_cts.Token));
                SetLifecycle(ProviderLifecycleState.Running, "Embedded observer waiting for tracked StarCraft process");
            }
        }

        public void SetTrackedGameProcess(int processId, DateTime processStartedAtUtc)
        {
            if (IsClosing) return;
            if (processId <= 0) throw new ArgumentOutOfRangeException(nameof(processId));
            if (processStartedAtUtc.Kind != DateTimeKind.Utc)
                processStartedAtUtc = processStartedAtUtc.ToUniversalTime();

            if (!_sessionBinding.TrySetTrackedProcess(processId, processStartedAtUtc))
                FailClosed("Tracked StarCraft process changed after the embedded observer session opened");
        }

        public void ClearTrackedGameProcess()
        {
            if (IsClosing) return;
            _sessionBinding.ClearTrackedProcess();
        }

        public void BeginStop()
        {
            lock (_lifecycleGate)
            {
                if (!_publication.TryBeginClosing()) return;
                SetLifecycle(ProviderLifecycleState.Stopping, "Stopping embedded BWRAPI observer");
                _publication.CompleteClosing();
                _cts.Cancel();
                _sessionBinding.SignalWaiters();
                _semanticWake.Set();
                _projectionWake.Set();
                _sessionBinding.CancelClientWait();
            }
        }

        public ProviderStopResult TryStop(TimeSpan timeout)
        {
            if (timeout < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout));
            BeginStop();
            bool stopped;
            try { stopped = _supervisor.IsCompleted || _supervisor.Wait(timeout); }
            catch (AggregateException) { stopped = _supervisor.IsCompleted; }
            if (!stopped)
            {
                SetLifecycle(ProviderLifecycleState.ShutdownFailed,
                    "Embedded observer did not stop within the bounded shutdown timeout");
                return new ProviderStopResult(Lifecycle);
            }

            CompleteDispose();
            SetLifecycle(ProviderLifecycleState.Stopped, "Embedded BWRAPI observer stopped");
            return new ProviderStopResult(Lifecycle);
        }

        public void Dispose()
        {
            BeginStop();
            TryStop(TimeSpan.FromMilliseconds(Math.Max(1, _config.ProviderShutdownTimeoutMs)));
        }

        private void FailClosed(string message)
        {
            _publication.PublishFatalFailure(message);
            BeginStop();
        }

        private void CompleteDispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            _sessionBinding.Dispose();
            _semanticWake.Dispose();
            _projectionWake.Dispose();
            _cts.Dispose();
        }

        private void SetLifecycle(ProviderLifecycleState state, string message)
        {
            Volatile.Write(ref _lifecycleMessage, message ?? string.Empty);
            Volatile.Write(ref _lifecycleState, (int)state);
        }
    }
}
