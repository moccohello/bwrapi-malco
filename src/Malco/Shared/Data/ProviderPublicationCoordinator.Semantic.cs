using System;
using System.Linq;
using System.Threading;
using BwrApi.Client;
using Malco.Models;

namespace Malco.Data
{
    internal sealed partial class ProviderPublicationCoordinator
    {
        public void PublishSemanticObservation(
            BwrApiFrameHeader header,
            BwrApiSemanticSnapshotV1 source)
        {
            if (IsClosing) return;
            var sessionEpoch = source.SessionEpoch ?? string.Empty;
            if (!string.Equals(
                    header.SessionEpoch ?? string.Empty,
                    sessionEpoch,
                    StringComparison.Ordinal) ||
                header.GameFrame != source.Frame)
            {
                throw new BwrApiNativeArtifactException(
                    "semantic_frame_correlation_invalid",
                    "The embedded frame header and semantic component do not describe the same session/frame.");
            }

            long generation = checked((long)source.SessionGeneration);
            lock (_gate)
            {
                if (IsClosing || _terminalPublication) return;
                bool semanticIdentityChanged = false;
                var sameEpoch = string.Equals(
                    _semanticEpoch,
                    sessionEpoch,
                    StringComparison.Ordinal);
                if (!sameEpoch ||
                    _semanticGeneration != generation)
                {
                    if (sameEpoch && generation < _semanticGeneration)
                    {
                        throw new BwrApiNativeArtifactException(
                            "semantic_generation_regressed",
                            "The embedded semantic session generation regressed.");
                    }

                    _semanticEpoch = sessionEpoch;
                    _semanticGeneration = generation;
                    ResetSemanticSessionState();
                    semanticIdentityChanged = true;
                }

                BwrApiRuntimeSnapshot runtime =
                    _semanticRuntimeMapper.Map(header, source);
                GameSnapshot snapshot =
                    _snapshotMapper.BuildSemanticSnapshot(runtime);
                ProviderStatus status =
                    ResolveSemanticStatus(header, source, runtime);
                string message = BuildSemanticMessage(source, status);
                snapshot = snapshot.WithWorkerStateStatus(message);
                var semantic = new SemanticSnapshotState(
                    status,
                    snapshot,
                    runtime.PublicationSequence,
                    checked((int)header.GameFrame),
                    GameSnapshotSemanticKey.Build(snapshot, status, message),
                    generation,
                    message,
                    isAuthoritativeOutOfMatch:
                        source.Session != null && !source.Session.GameReady,
                    sessionEpoch: sessionEpoch);
                ProviderChannelState before = Volatile.Read(ref _channels);
                ProviderChannelState after =
                    ProviderChannelStateUpdater.UpdateSemantic(
                        ref _channels,
                        semantic);
                bool sessionChanged =
                    semanticIdentityChanged ||
                    before.Semantic.SessionGeneration != generation ||
                    !string.Equals(
                        before.Semantic.SessionEpoch,
                        sessionEpoch,
                        StringComparison.Ordinal);
                bool projectionChannelsReset =
                    sessionChanged ||
                    !ReferenceEquals(before.Commands, after.Commands) ||
                    !ReferenceEquals(before.Viewport, after.Viewport);
                if (projectionChannelsReset)
                {
                    _projectionMailbox.Publish(after.Viewport);
                    _metrics.RecordProjectionChannelResetCommits();
                }

                _metrics.RecordSemanticCommit();
                _commitSink?.MarkProviderCommit(
                    projectionChannelsReset
                        ? ProviderCommitMask.Semantic |
                          ProviderCommitMask.Commands |
                          ProviderCommitMask.ProjectionControl
                        : ProviderCommitMask.Semantic);
            }
        }

        private void ResetSemanticSessionState()
        {
            _snapshotMapper.ResetSessionState();
            _semanticRuntimeMapper.ResetSessionState();
        }

        private static ProviderStatus ResolveSemanticStatus(
            BwrApiFrameHeader header,
            BwrApiSemanticSnapshotV1 source,
            BwrApiRuntimeSnapshot runtime)
        {
            if (header.ErrorKind != BwrApiErrorKind.None &&
                !(header.ErrorKind == BwrApiErrorKind.Unavailable &&
                  (source.Status == BwrApiSemanticSnapshotStatus.Partial ||
                   header.DataState == BwrApiDataState.Stale)))
            {
                return ProviderStatus.Error;
            }

            if (source.Status == BwrApiSemanticSnapshotStatus.Unavailable)
                return ProviderStatus.NotReady;
            if (!runtime.IsInMatch || runtime.PerspectivePlayerId < 0)
                return ProviderStatus.NotReady;
            if (runtime.Race == Race.Unknown ||
                !runtime.HasReliableUpgradeState)
                return ProviderStatus.Stale;
            if (HasUnknownLocalUpgradeFacts(
                    source,
                    runtime.PerspectivePlayerId))
                return ProviderStatus.Stale;
            return header.DataState == BwrApiDataState.Complete &&
                   source.Status == BwrApiSemanticSnapshotStatus.Complete
                ? ProviderStatus.Ready
                : ProviderStatus.Stale;
        }

        private static bool HasUnknownLocalUpgradeFacts(
            BwrApiSemanticSnapshotV1 source,
            int localPlayerId)
        {
            return source.Upgrades.Any(upgrade =>
                       upgrade.PlayerId == localPlayerId &&
                       (!upgrade.Level.HasValue ||
                        !upgrade.InProgress.HasValue ||
                        (upgrade.InProgress == true &&
                         !upgrade.RemainingFrames.HasValue))) ||
                   source.Techs.Any(tech =>
                       tech.PlayerId == localPlayerId &&
                       (!tech.Researched.HasValue ||
                        !tech.Available.HasValue ||
                        !tech.InProgress.HasValue ||
                        (tech.InProgress == true &&
                         !tech.RemainingFrames.HasValue)));
        }

        private static string BuildSemanticMessage(
            BwrApiSemanticSnapshotV1 source,
            ProviderStatus status)
        {
            string upstream = source.Message ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(upstream)) return upstream;
            return status switch
            {
                ProviderStatus.Ready =>
                    "Embedded BWRAPI observer ready",
                ProviderStatus.Stale =>
                    "Embedded BWRAPI observer data is stale",
                ProviderStatus.Error =>
                    "Embedded BWRAPI observer error",
                _ => "Embedded BWRAPI observer waiting"
            };
        }
    }
}
