using System;
using System.Threading;
using BwrApi.Client;
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
            lock (_gate)
            {
                if (IsClosing || _terminalPublication) return;
                ProviderChannelState state = Volatile.Read(ref _channels);
                long generation = Math.Max(
                    0,
                    state.Semantic.SessionGeneration);

                if (previousDemand.NeedsProjection != demand.NeedsProjection)
                {
                    PublishViewportLocked(
                        ViewportProjectionState.Unavailable(
                            demand.NeedsProjection
                                ? "Waiting for a fresh viewport observation"
                                : "Viewport projection disabled",
                            generation,
                            PendingProjectionRevision,
                            epoch,
                            ProviderStatus.NotReady,
                            state.Semantic.SessionEpoch,
                            true,
                            ProjectionClearReason.DemandChanged));
                }

                if (previousDemand.NeedsCommands != demand.NeedsCommands)
                {
                    PublishCommandsLocked(
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
            }
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
            lock (_gate)
            {
                if (IsClosing || _terminalPublication) return;
                PublishViewportLocked(viewport);
            }
        }

        private void PublishCommands(CommandProjectionState commands)
        {
            if (IsClosing) return;
            lock (_gate)
            {
                if (IsClosing || _terminalPublication) return;
                PublishCommandsLocked(commands);
            }
        }

        private void PublishViewportLocked(
            ViewportProjectionState viewport)
        {
            ProviderChannelState before = Volatile.Read(ref _channels);
            ProviderChannelState after =
                ProviderChannelStateUpdater.UpdateViewport(
                    ref _channels,
                    viewport);
            if (ReferenceEquals(before, after)) return;
            _projectionMailbox.Publish(viewport);
            _metrics.RecordProjectionCommit();
            _commitSink?.MarkProviderCommit(
                ProviderCommitMask.ProjectionControl);
        }

        private void PublishCommandsLocked(
            CommandProjectionState commands)
        {
            ProviderChannelState before = Volatile.Read(ref _channels);
            ProviderChannelState after =
                ProviderChannelStateUpdater.UpdateCommands(
                    ref _channels,
                    commands);
            if (ReferenceEquals(before, after)) return;
            _metrics.RecordCommandCommit();
            _commitSink?.MarkProviderCommit(ProviderCommitMask.Commands);
        }
    }
}
