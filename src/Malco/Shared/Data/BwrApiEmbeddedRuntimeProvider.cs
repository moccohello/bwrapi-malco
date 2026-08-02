using System;
using System.Threading;
using System.Threading.Tasks;
using Malco.Application.Contracts.Projection;
using Malco.Application.Demand;
using Malco.Configuration;

namespace Malco.Data
{
    /// <summary>
    /// Read-only Malco adapter over the public embedded BWRAPI observer package.
    /// The public SDK owns verification and loading of the product-local runtime;
    /// Malco never P/Invokes a runtime DLL or exposes controller authority. The
    /// shell supplies the exact retained SCR process identity.
    /// </summary>
    internal sealed partial class BwrApiEmbeddedRuntimeProvider :
        IGameDataProviderLifecycle,
        IProviderChannelStateProvider,
        IProjectionMailboxSource,
        IProviderCommitSignalSource,
        IOverlayDemandController,
        IProviderOptimizationMetricsSource,
        ITrackedGameProcessSink,
        IDisposable
    {
        private static readonly TimeSpan NativePollTimeout = TimeSpan.FromMilliseconds(1);
        private static readonly TimeSpan ViewportActiveInterval =
            TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 240);
        private static readonly TimeSpan CommandActiveInterval =
            TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 24);

        private readonly OverlayConfig _config;
        private readonly EmbeddedObserverMetrics _metrics;
        private readonly ProviderPublicationCoordinator _publication;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly TrackedObserverSessionBinding _sessionBinding =
            new TrackedObserverSessionBinding();
        private readonly AutoResetEvent _semanticWake = new AutoResetEvent(false);
        private readonly AutoResetEvent _projectionWake = new AutoResetEvent(false);
        private readonly object _lifecycleGate = new object();
        private readonly object _demandGate = new object();
        private Task _supervisor = Task.CompletedTask;
        private OverlayChannelDemand _demand = OverlayChannelDemand.All;
        private int _lifecycleState = (int)ProviderLifecycleState.Created;
        private string _lifecycleMessage = "Embedded observer created; waiting for tracked StarCraft process";
        private int _disposed;
        private long _demandEpoch;

        public BwrApiEmbeddedRuntimeProvider(OverlayConfig config)
        {
            _config = config ?? new OverlayConfig();
            _metrics = new EmbeddedObserverMetrics();
            _publication = new ProviderPublicationCoordinator(_metrics);
        }

        public ProviderLifecycleSnapshot Lifecycle => new ProviderLifecycleSnapshot(
            (ProviderLifecycleState)Volatile.Read(ref _lifecycleState),
            Volatile.Read(ref _lifecycleMessage));

        public IProjectionMailboxReader ProjectionMailboxReader =>
            _publication.ProjectionMailboxReader;

        public ProviderChannelState GetProviderChannelState() =>
            _publication.GetProviderChannelState();

        public void RegisterCommitSink(IProviderCommitSink sink) =>
            _publication.RegisterCommitSink(sink);

        public void UnregisterCommitSink(IProviderCommitSink sink) =>
            _publication.UnregisterCommitSink(sink);

        public void RegisterProjectionPresentationCommitSink(IProjectionPresentationCommitSink sink) =>
            _publication.RegisterProjectionPresentationCommitSink(sink);

        public void UnregisterProjectionPresentationCommitSink(IProjectionPresentationCommitSink sink) =>
            _publication.UnregisterProjectionPresentationCommitSink(sink);

        public ProviderOptimizationCounters GetOptimizationCounters() =>
            _metrics.GetOptimizationCounters();

        public ProviderPerformanceCounters GetPerformanceCounters() =>
            _metrics.GetPerformanceCounters();

        private bool IsClosing => _publication.IsClosing;
    }
}
