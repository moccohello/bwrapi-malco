using System;

namespace Malco.Data
{
    internal enum ProviderLifecycleState
    {
        Created,
        Starting,
        Running,
        Stopping,
        Stopped,
        ShutdownFailed
    }

    internal readonly struct ProviderLifecycleSnapshot
    {
        public ProviderLifecycleSnapshot(ProviderLifecycleState state, string message)
        {
            State = state;
            Message = message ?? string.Empty;
        }

        public ProviderLifecycleState State { get; }
        public string Message { get; }
        public bool IsStopped => State == ProviderLifecycleState.Stopped;
        public bool IsShutdownFailed => State == ProviderLifecycleState.ShutdownFailed;
    }

    internal readonly struct ProviderStopResult
    {
        public ProviderStopResult(ProviderLifecycleSnapshot lifecycle)
        {
            Lifecycle = lifecycle;
        }

        public ProviderLifecycleSnapshot Lifecycle { get; }
        public bool IsComplete => Lifecycle.IsStopped;
        public string Message => Lifecycle.Message;
    }

    internal interface IGameDataProviderLifecycle
    {
        ProviderLifecycleSnapshot Lifecycle { get; }

        void Start();

        void BeginStop();

        ProviderStopResult TryStop(TimeSpan timeout);
    }

    // The shell is the sole owner of StarCraft HWND/process discovery. Embedded
    // observers receive only the exact retained PID/start-time identity.
    internal interface ITrackedGameProcessSink
    {
        void SetTrackedGameProcess(int processId, DateTime processStartedAtUtc);

        void ClearTrackedGameProcess();
    }
}
