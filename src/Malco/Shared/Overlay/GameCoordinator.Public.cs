using System;
using System.Threading;
using Malco.Application.Contracts.Output;
using Malco.Application.Contracts.Projection;
using Malco.Application.Demand;
using Malco.Application.Scheduling;
using Malco.Data;

namespace Malco.Game.Services
{
    internal sealed partial class GameCoordinator
    {
        public OverlayDemandReceipt SetDemand(OverlayChannelDemand demand)
        {
            if (demand == null) throw new ArgumentNullException(nameof(demand));
            lock (_disposeSync)
            {
                if (IsClosing)
                {
                    return new OverlayDemandReceipt(Volatile.Read(ref _lastDemandEpoch), _currentDemand);
                }

                var receipt = _demandController.SetDemand(demand);
                _currentDemand = receipt.Demand;
                Volatile.Write(ref _lastDemandEpoch, receipt.Epoch);
                return receipt;
            }
        }

        public void RegisterStateCommitSink(IOverlayStateCommitSink sink)
        {
            if (sink == null) throw new ArgumentNullException(nameof(sink));
            lock (_publicationSync)
            {
                if (IsClosing) return;
                if (!_stateCommitSinks.Contains(sink)) _stateCommitSinks.Add(sink);
            }
            sink.MarkOverlayStateCommitted(Latest);
        }

        public void UnregisterStateCommitSink(IOverlayStateCommitSink sink)
        {
            lock (_publicationSync)
            {
                _stateCommitSinks.Remove(sink);
            }
        }

        public IProjectionMailboxReader ProjectionMailboxReader
        {
            get { return _projectionMailboxSource.ProjectionMailboxReader; }
        }

        public void RegisterProjectionPresentationCommitSink(IProjectionPresentationCommitSink sink)
        {
            if (sink == null) throw new System.ArgumentNullException(nameof(sink));
            lock (_publicationSync)
            {
                if (!IsClosing) _projectionMailboxSource.RegisterProjectionPresentationCommitSink(sink);
            }
        }

        public void UnregisterProjectionPresentationCommitSink(IProjectionPresentationCommitSink sink)
        {
            lock (_publicationSync)
            {
                _projectionMailboxSource.UnregisterProjectionPresentationCommitSink(sink);
            }
        }

        public OverlayReadModel Latest
        {
            get { return Volatile.Read(ref _stableOverlayState); }
        }

        public CoordinatorMetricsSnapshot GetMetricsSnapshot()
        {
            return new CoordinatorMetricsSnapshot(
                Volatile.Read(ref _semanticPublications),
                Volatile.Read(ref _commandPublications),
                Volatile.Read(ref _viewportPublications),
                Volatile.Read(ref _envelopePublications),
                Volatile.Read(ref _reducerNoOps));
        }

        public bool ShutdownBlocked
        {
            get { return Volatile.Read(ref _shutdownBlocked) != 0; }
        }

        public bool IsShutdownComplete
        {
            get { return Volatile.Read(ref _disposeCompleted) != 0; }
        }

        public string ShutdownFailureMessage
        {
            get { return Volatile.Read(ref _shutdownFailureMessage) ?? string.Empty; }
        }

        public void ClearStableSnapshot(string message)
        {
            var normalizedMessage = message ?? string.Empty;
            lock (_publicationSync)
            {
                if (IsClosing)
                {
                    return;
                }

                Volatile.Write(ref _clearProviderCommitEpoch, Volatile.Read(ref _providerCommitEpoch));
                Interlocked.Increment(ref _clearRequestEpoch);
                var previous = Interlocked.Exchange(
                    ref _stableOverlayState,
                    OverlayReadModel.Empty(normalizedMessage));
                if (previous != null)
                {
                    Interlocked.Increment(ref _semanticPublications);
                    Interlocked.Increment(ref _commandPublications);
                    Interlocked.Increment(ref _viewportPublications);
                    Interlocked.Increment(ref _envelopePublications);
                    MarkStateCommittedLocked();
                }
            }
            MarkDirty(ApplicationInputDirtyMask.ClearStableState);
        }
    }
}
