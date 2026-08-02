using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Malco.Application.Contracts.Output;
using Malco.Application.Contracts.Projection;
using Malco.Application.Demand;
using Malco.Application.Scheduling;
using Malco.Data;

namespace Malco.Game.Services
{
    internal sealed partial class GameCoordinator : IOverlayReadModelSource, IOverlayStateCommitSource, IProjectionMailboxSource, IProviderCommitSink, IOverlayDemandController, IDisposable
    {
        private readonly IGameDataProviderLifecycle _providerLifecycle;
        private readonly IProviderChannelStateProvider _channelProvider;
        private readonly IProjectionMailboxSource _projectionMailboxSource;
        private readonly IProviderCommitSignalSource _providerCommitSource;
        private readonly IOverlayDemandController _demandController;
        private readonly AutoResetEvent _workerWake = new AutoResetEvent(false);
        private readonly Task _dataCollectionTask;
        private readonly object _publicationSync = new object();
        private readonly object _disposeSync = new object();
        private readonly List<IOverlayStateCommitSink> _stateCommitSinks = new List<IOverlayStateCommitSink>();
        private readonly TimeSpan _shutdownTimeout;
        private OverlayReadModel _stableOverlayState;
        private long _semanticPublications;
        private long _commandPublications;
        private long _viewportPublications;
        private long _envelopePublications;
        private long _reducerNoOps;
        private int _closing;
        private int _disposeCompleted;
        private int _shutdownBlocked;
        private int _rawProviderDisposed;
        private int _dirtyMask;
        private string _shutdownFailureMessage = string.Empty;
        private long _providerCommitEpoch;
        private long _clearRequestEpoch;
        private long _clearProviderCommitEpoch;
        private long _lastDemandEpoch;
        private OverlayChannelDemand _currentDemand = OverlayChannelDemand.All;

        public GameCoordinator(
            IGameDataProviderLifecycle providerLifecycle,
            IProviderChannelStateProvider channelProvider,
            IProjectionMailboxSource projectionMailboxSource,
            IProviderCommitSignalSource providerCommitSource,
            IOverlayDemandController demandController,
            int providerShutdownTimeoutMs)
        {
            _providerLifecycle = providerLifecycle ?? throw new ArgumentNullException(nameof(providerLifecycle));
            _channelProvider = channelProvider ?? throw new ArgumentNullException(nameof(channelProvider));
            _projectionMailboxSource = projectionMailboxSource ?? throw new ArgumentNullException(nameof(projectionMailboxSource));
            _providerCommitSource = providerCommitSource ?? throw new ArgumentNullException(nameof(providerCommitSource));
            _demandController = demandController ?? throw new ArgumentNullException(nameof(demandController));
            _shutdownTimeout = TimeSpan.FromMilliseconds(Math.Max(1, providerShutdownTimeoutMs));
            _stableOverlayState = OverlayReadModel.Empty("No snapshot collected");
            _providerCommitSource.RegisterCommitSink(this);
            MarkDirty(
                ApplicationInputDirtyMask.Semantic |
                ApplicationInputDirtyMask.Commands |
                ApplicationInputDirtyMask.ProjectionControl);
            _dataCollectionTask = Task.Factory.StartNew(
                DataCollectionLoop,
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }

    }

    internal readonly struct CoordinatorMetricsSnapshot
    {
        public CoordinatorMetricsSnapshot(long semantic, long commands, long viewport, long envelopes, long reducerNoOps)
        {
            SemanticPublications = semantic;
            CommandPublications = commands;
            ViewportPublications = viewport;
            EnvelopePublications = envelopes;
            ReducerNoOps = reducerNoOps;
        }

        public long SemanticPublications { get; }
        public long CommandPublications { get; }
        public long ViewportPublications { get; }
        public long EnvelopePublications { get; }
        public long ReducerNoOps { get; }
    }
}
