using System;
using System.Threading;
using Malco.Application.Scheduling;
using Malco.Data;
using Malco.Models;

namespace Malco.Game.Services
{
    internal sealed partial class GameCoordinator
    {
        public void MarkProviderCommit(ProviderCommitMask mask)
        {
            if (mask == ProviderCommitMask.None || IsClosing)
            {
                return;
            }

            var dirty = MapProviderCommitMask(mask);
            if (dirty == ApplicationInputDirtyMask.None)
            {
                return;
            }

            Interlocked.Increment(ref _providerCommitEpoch);
            MarkDirty(dirty);
        }

        private static ApplicationInputDirtyMask MapProviderCommitMask(
            ProviderCommitMask mask)
        {
            var dirty = ApplicationInputDirtyMask.None;
            if ((mask & ProviderCommitMask.Semantic) != 0)
            {
                dirty |= ApplicationInputDirtyMask.Semantic;
            }
            if ((mask & ProviderCommitMask.Commands) != 0)
            {
                dirty |= ApplicationInputDirtyMask.Commands;
            }
            if ((mask & ProviderCommitMask.ProjectionControl) != 0)
            {
                dirty |= ApplicationInputDirtyMask.ProjectionControl;
            }
            return dirty;
        }

        private void DataCollectionLoop()
        {
            while (!IsClosing)
            {
                if (Volatile.Read(ref _dirtyMask) == 0)
                {
                    _workerWake.WaitOne();
                }

                if (IsClosing)
                {
                    return;
                }

                DrainDirtyWork();
            }
        }

        private void MarkDirty(ApplicationInputDirtyMask mask)
        {
            if (mask == ApplicationInputDirtyMask.None || IsClosing)
            {
                return;
            }

            Interlocked.Or(ref _dirtyMask, (int)mask);
            _workerWake.Set();
        }

        private void DrainDirtyWork()
        {
            while (!IsClosing)
            {
                // Capture the clear fence before taking ownership of queued work.
                // A UI clear that races this pass must invalidate everything this
                // pass read, even when it happens before UpdateData begins.
                var workClearEpoch = Volatile.Read(ref _clearRequestEpoch);
                var dirty = (ApplicationInputDirtyMask)Interlocked.Exchange(ref _dirtyMask, 0);
                if (dirty == ApplicationInputDirtyMask.None)
                {
                    return;
                }
                var workProviderCommitEpoch = Volatile.Read(ref _providerCommitEpoch);

                var clearRequested = (dirty & ApplicationInputDirtyMask.ClearStableState) != 0;
                if (clearRequested)
                {
                    // The clear bit may have been queued after the fence capture but
                    // before the dirty exchange. From here onward this pass belongs
                    // to the clear boundary just observed; only a post-boundary provider
                    // commit may repopulate state.
                    workClearEpoch = Volatile.Read(ref _clearRequestEpoch);
                }
                var providerDirty = (dirty & (
                    ApplicationInputDirtyMask.Semantic |
                    ApplicationInputDirtyMask.Commands |
                    ApplicationInputDirtyMask.ProjectionControl)) != 0;
                if (providerDirty && workClearEpoch != 0)
                {
                    providerDirty = workProviderCommitEpoch >
                                    Volatile.Read(ref _clearProviderCommitEpoch);
                }
                try
                {
                    UpdateData(providerDirty, workClearEpoch);
                }
                catch (Exception ex)
                {
                    if (IsClosing)
                    {
                        return;
                    }
                    PublishPollingError("Provider polling failed: " + ex.Message, workClearEpoch);
                }
            }
        }

        private void UpdateData(bool providerDirty, long clearEpoch)
        {
            if (IsClosing)
            {
                return;
            }

            var current = Volatile.Read(ref _stableOverlayState) ?? OverlayReadModel.Empty("No overlay state collected");
            var provider = providerDirty ? ReadProviderChannels() : null;
            var next = providerDirty
                ? OverlayStateReducer.Compose(current, provider)
                : current;
            lock (_publicationSync)
            {
                if (IsClosing)
                {
                    return;
                }
                if (clearEpoch != Volatile.Read(ref _clearRequestEpoch))
                {
                    // ClearStableSnapshot published its immutable empty state while this
                    // pass was computing. Never allow the pre-clear result to overwrite it.
                    if (Volatile.Read(ref _providerCommitEpoch) >
                        Volatile.Read(ref _clearProviderCommitEpoch))
                    {
                        MarkDirty(
                            ApplicationInputDirtyMask.Semantic |
                            ApplicationInputDirtyMask.Commands |
                            ApplicationInputDirtyMask.ProjectionControl);
                    }
                    return;
                }

                if (!ReferenceEquals(next, current))
                {
                    if (!ReferenceEquals(next.Semantic, current.Semantic)) Interlocked.Increment(ref _semanticPublications);
                    if (!ReferenceEquals(next.Commands, current.Commands)) Interlocked.Increment(ref _commandPublications);
                    if (!ReferenceEquals(next.Viewport, current.Viewport)) Interlocked.Increment(ref _viewportPublications);
                    Interlocked.Increment(ref _envelopePublications);
                    Interlocked.Exchange(ref _stableOverlayState, next);
                    MarkStateCommittedLocked();
                }
                else
                {
                    Interlocked.Increment(ref _reducerNoOps);
                }
            }
        }

        private ProviderChannelState ReadProviderChannels()
        {
            return _channelProvider.GetProviderChannelState();
        }

        private void PublishPollingError(string message, long clearEpoch)
        {
            lock (_publicationSync)
            {
                if (IsClosing || clearEpoch != Volatile.Read(ref _clearRequestEpoch))
                {
                    return;
                }

                var current = Volatile.Read(ref _stableOverlayState) ?? OverlayReadModel.Empty(message);
                var currentSnapshot = current.Semantic.Snapshot;
                var semantic = current.Semantic.WithStatus(
                    ProviderStatus.Error,
                    current.Semantic.Sequence,
                    current.Semantic.Frame,
                    GameSnapshotSemanticKey.Build(currentSnapshot, ProviderStatus.Error, message),
                    message);
                var next = new OverlayReadModel(semantic, current.Commands, current.Viewport);
                Interlocked.Exchange(ref _stableOverlayState, next);
                Interlocked.Increment(ref _semanticPublications);
                Interlocked.Increment(ref _envelopePublications);
                MarkStateCommittedLocked();
            }
        }

        private void MarkStateCommittedLocked()
        {
            foreach (var sink in _stateCommitSinks.ToArray())
            {
                sink.MarkOverlayStateCommitted(_stableOverlayState);
            }
        }
    }
}
