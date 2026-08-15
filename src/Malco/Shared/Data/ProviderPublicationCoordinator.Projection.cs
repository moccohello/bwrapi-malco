using System;
using System.Threading;
using BwrApi.Client;
using Malco.Application.Contracts.Projection;
using Malco.Application.Demand;

namespace Malco.Data
{
    internal sealed partial class ProviderPublicationCoordinator
    {
        public void ApplyDemandChange(
            OverlayChannelDemand previousDemand,
            OverlayChannelDemand demand,
            long epoch)
        {
            if (IsClosing) return;
            var viewportChanged = false;
            var commandsChanged = false;
            ViewportProjectionState viewport = null;
            IProviderCommitSink commitSink = null;
            IProjectionPresentationCommitSink presentationCommitSink = null;
            lock (_gate)
            {
                if (IsClosing || _terminalPublication) return;
                ProviderChannelState state = Volatile.Read(ref _channels);
                long generation = Math.Max(
                    0,
                    state.Semantic.SessionGeneration);

                if (previousDemand.NeedsProjection != demand.NeedsProjection)
                {
                    viewport = ViewportProjectionState.Unavailable(
                        demand.NeedsProjection
                            ? "Waiting for a fresh viewport observation"
                            : "Viewport projection disabled",
                        generation,
                        PendingProjectionRevision,
                        epoch,
                        ProviderStatus.NotReady,
                        state.Semantic.SessionEpoch,
                        true,
                        ProjectionClearReason.DemandChanged);
                    viewportChanged = PublishViewportLocked(
                        viewport,
                        out presentationCommitSink);
                }

                if (previousDemand.NeedsCommands != demand.NeedsCommands)
                {
                    commandsChanged = PublishCommandsLocked(
                        CommandProjectionState.Unavailable(
                            generation,
                            demand.NeedsCommands
                                ? "Waiting for a fresh coherent command observation"
                                : "Command projection disabled",
                            PendingProjectionRevision,
                            SelectionCompleteness.Unknown,
                            epoch,
                            demand.NeedsCommands,
                            true,
                            state.Semantic.SessionEpoch,
                            clearReason: ProjectionClearReason.DemandChanged));
                }
                commitSink = _commitSink;
            }
            presentationCommitSink?.MarkProjectionPresentationCommitted();
            if (viewportChanged)
                commitSink?.MarkProviderCommit(ProviderCommitMask.ProjectionControl);
            if (commandsChanged)
                commitSink?.MarkProviderCommit(ProviderCommitMask.Commands);
        }

        public void PublishViewportObservation(BwrApiViewportProjectionV1 source)
        {
            PublishViewport(ProviderProjectionMapper.MapViewport(source));
        }

        public void PublishCommandObservation(
            BwrApiSelectedCommandProjectionV1 source)
        {
            PublishCommands(
                ProviderProjectionMapper.MapCommand(
                    source,
                    () => Volatile.Read(ref _channels).Commands));
        }

        private void PublishViewport(ViewportProjectionState viewport)
        {
            if (IsClosing) return;
            var changed = false;
            IProviderCommitSink commitSink = null;
            IProjectionPresentationCommitSink presentationCommitSink = null;
            lock (_gate)
            {
                if (IsClosing || _terminalPublication) return;
                changed = PublishViewportLocked(
                    viewport,
                    out presentationCommitSink);
                commitSink = _commitSink;
            }
            if (!changed) return;
            presentationCommitSink?.MarkProjectionPresentationCommitted();
            commitSink?.MarkProviderCommit(
                ProviderCommitMask.ProjectionControl);
        }

        private void PublishCommands(CommandProjectionState commands)
        {
            if (IsClosing) return;
            var changed = false;
            IProviderCommitSink commitSink = null;
            lock (_gate)
            {
                if (IsClosing || _terminalPublication) return;
                changed = PublishCommandsLocked(commands);
                commitSink = _commitSink;
            }
            if (changed)
                commitSink?.MarkProviderCommit(ProviderCommitMask.Commands);
        }

        private bool PublishViewportLocked(
            ViewportProjectionState viewport,
            out IProjectionPresentationCommitSink presentationCommitSink)
        {
            presentationCommitSink = null;
            ProviderChannelState before = Volatile.Read(ref _channels);
            ProviderChannelState after =
                ProviderChannelStateUpdater.UpdateViewport(
                    ref _channels,
                    viewport);
            if (ReferenceEquals(before, after)) return false;
            _projectionMailbox.Commit(
                viewport,
                out presentationCommitSink);
            _metrics.RecordProjectionCommit();
            return true;
        }

        private bool PublishCommandsLocked(
            CommandProjectionState commands)
        {
            ProviderChannelState before = Volatile.Read(ref _channels);
            ProviderChannelState after =
                ProviderChannelStateUpdater.UpdateCommands(
                    ref _channels,
                    commands);
            if (ReferenceEquals(before, after)) return false;
            _metrics.RecordCommandCommit();
            return true;
        }
    }
}
