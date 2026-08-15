using System;
using System.Threading;
using Malco.Application.Contracts.Projection;
using Malco.Models;

namespace Malco.Data
{
    internal sealed partial class ProviderPublicationCoordinator
    {
        public void PublishSemanticFailure(
            string message,
            ProviderStatus status = ProviderStatus.Error)
        {
            if (IsClosing) return;
            IProviderCommitSink commitSink = null;
            lock (_gate)
            {
                if (IsClosing || _terminalPublication) return;
                var current = Volatile.Read(ref _channels).Semantic;
                var normalizedMessage = message ?? string.Empty;
                if (current.Status == status &&
                    string.Equals(
                        current.Message,
                        normalizedMessage,
                        StringComparison.Ordinal))
                {
                    return;
                }
                var snapshot = current.Snapshot;
                var semantic = current.WithStatus(
                    status,
                    current.Sequence,
                    current.Frame,
                    GameSnapshotSemanticKey.Build(snapshot, status, message),
                    message);
                ProviderChannelStateUpdater.UpdateSemanticStatus(
                    ref _channels,
                    semantic);
                _metrics.RecordSemanticCommit();
                commitSink = _commitSink;
            }
            commitSink?.MarkProviderCommit(ProviderCommitMask.Semantic);
        }

        public void PublishViewportFailure(
            string message,
            ProviderStatus status)
        {
            if (IsClosing) return;
            var state = Volatile.Read(ref _channels);
            if (!state.Viewport.IsUsable &&
                !state.Viewport.IsAuthoritativeClear &&
                state.Viewport.Status == status &&
                string.Equals(
                    state.Viewport.Message,
                    message ?? string.Empty,
                    StringComparison.Ordinal))
            {
                return;
            }
            var failure = ViewportProjectionState.Unavailable(
                message,
                state.Semantic.SessionGeneration,
                state.Viewport.Revision + 1,
                state.Viewport.DemandEpoch,
                status,
                state.Semantic.SessionEpoch);
            PublishViewport(failure);
        }

        public void PublishCommandFailure(string message)
        {
            if (IsClosing) return;
            var state = Volatile.Read(ref _channels);
            var current = state.Commands;
            if (current.Status == CommandObservationStatus.Error &&
                string.Equals(
                    current.Message,
                    message ?? string.Empty,
                    StringComparison.Ordinal))
            {
                return;
            }
            var retainsContent =
                current.IsCoherent || current.RetainsPreviousContent;
            var failure = new CommandProjectionState(
                CommandObservationStatus.Error,
                retainsContent ? current.Lines : null,
                retainsContent ? current.SelectedUnitTags : null,
                retainsContent ? current.Sequence : null,
                retainsContent ? current.Frame : null,
                retainsContent ? current.Key : string.Empty,
                retainsContent ? current.BaseSemanticSequence : null,
                state.Semantic.SessionGeneration,
                message,
                current.Revision + 1,
                current.SelectionCompleteness,
                current.DemandEpoch,
                current.IsDemanded,
                false,
                state.Semantic.SessionEpoch,
                retainsContent);
            PublishCommands(failure);
        }

        public void PublishFatalFailure(string message)
        {
            if (IsClosing) return;
            IProviderCommitSink commitSink = null;
            IProjectionPresentationCommitSink presentationCommitSink = null;
            lock (_gate)
            {
                if (IsClosing || _terminalPublication) return;
                _terminalPublication = true;
                var current = Volatile.Read(ref _channels);
                var currentSemantic = current.Semantic;
                var sessionEpoch = currentSemantic.SessionEpoch;
                var generation = currentSemantic.SessionGeneration;
                GameSnapshot snapshot = GameSnapshotFactory.NotReady(message);
                _snapshotMapper.ResetSessionState();
                var semantic = new SemanticSnapshotState(
                    ProviderStatus.Error,
                    snapshot,
                    currentSemantic.Sequence,
                    currentSemantic.Frame,
                    GameSnapshotSemanticKey.Build(
                        snapshot,
                        ProviderStatus.Error,
                        message),
                    generation,
                    message,
                    isAuthoritativeOutOfMatch: true,
                    sessionEpoch: sessionEpoch);
                var commands = CommandProjectionState.Unavailable(
                    generation,
                    message,
                    current.Commands.Revision + 1,
                    SelectionCompleteness.Unknown,
                    current.Commands.DemandEpoch,
                    current.Commands.IsDemanded,
                    true,
                    sessionEpoch,
                    clearReason: ProjectionClearReason.SourceReset);
                var viewport = ViewportProjectionState.Unavailable(
                    message,
                    generation,
                    current.Viewport.Revision + 1,
                    current.Viewport.DemandEpoch,
                    ProviderStatus.Error,
                    sessionEpoch,
                    true,
                    ProjectionClearReason.SourceReset);
                var next = new ProviderChannelState(
                    semantic,
                    commands,
                    viewport,
                    DateTime.UtcNow);
                Volatile.Write(ref _channels, next);
                _projectionMailbox.Commit(
                    next.Viewport,
                    out presentationCommitSink);
                _metrics.RecordFatalCommits();
                commitSink = _commitSink;
            }
            presentationCommitSink?.MarkProjectionPresentationCommitted();
            commitSink?.MarkProviderCommit(
                ProviderCommitMask.Semantic |
                ProviderCommitMask.Commands |
                ProviderCommitMask.ProjectionControl);
        }
    }
}
