using System;
using Malco.Application.Contracts;
using Malco.Data;
using Malco.Models;

namespace Malco.Game.Services
{
    internal static class OverlayStateReducer
    {
        public static SemanticSnapshotState SelectSemantic(
            SemanticSnapshotState current,
            SemanticSnapshotState incoming)
        {
            if (incoming == null)
            {
                return current ?? SemanticSnapshotState.Empty("No semantic state collected");
            }

            if (ReferenceEquals(current, incoming))
            {
                return current;
            }

            // A producer-authenticated match end or terminal source reset is a
            // destructive state transition, not a transient read failure. It
            // must never be rewritten onto the last in-match snapshot.
            if (incoming.IsAuthoritativeOutOfMatch)
            {
                return incoming;
            }

            if (current == null ||
                incoming.Status == ProviderStatus.Ready ||
                incoming.SessionGeneration != current.SessionGeneration ||
                !string.Equals(incoming.SessionEpoch, current.SessionEpoch, StringComparison.Ordinal))
            {
                return incoming;
            }

            var currentSnapshot = current.Snapshot;
            if (currentSnapshot == null)
            {
                return incoming;
            }

            var incomingSnapshot = incoming.Snapshot;
            if (incoming.Status == ProviderStatus.Stale &&
                incomingSnapshot != null &&
                incomingSnapshot.IsInMatch &&
                incomingSnapshot.LocalPlayerId >= 0 &&
                incomingSnapshot.Race != Race.Unknown &&
                !ReferenceEquals(currentSnapshot, incomingSnapshot))
            {
                return incoming;
            }

            var key = GameSnapshotSemanticKey.Build(currentSnapshot, incoming.Status, incoming.Message);
            if (current.Status == incoming.Status &&
                string.Equals(current.Message, incoming.Message, StringComparison.Ordinal) &&
                string.Equals(current.Key, key, StringComparison.Ordinal))
            {
                return current;
            }

            return current.WithStatus(
                incoming.Status,
                current.Sequence,
                current.Frame,
                key,
                incoming.Message);
        }

        public static CommandProjectionState SelectCommands(
            FrozenSemanticSnapshot semanticSnapshot,
            CommandProjectionState incoming,
            CommandProjectionState current,
            string sessionEpoch,
            long generation)
        {
            var incomingMatches = MatchesSession(incoming, sessionEpoch, generation);
            var currentMatches = MatchesSession(current, sessionEpoch, generation);
            if (incomingMatches && currentMatches && incoming.DemandEpoch < current.DemandEpoch)
            {
                return current;
            }

            if (incomingMatches &&
                (incoming.IsAuthoritativeClear || !incoming.IsDemanded ||
                 !currentMatches || incoming.DemandEpoch > current.DemandEpoch))
            {
                return incoming;
            }

            if (semanticSnapshot == null || !semanticSnapshot.IsInMatch)
            {
                return incomingMatches
                    ? incoming
                    : currentMatches && current.Status == CommandObservationStatus.Unavailable
                    ? current
                    : CommandProjectionState.Unavailable(
                        generation,
                        "No active match",
                        sessionEpoch: sessionEpoch);
            }

            if (incomingMatches && incoming.IsCoherent)
            {
                return incoming;
            }

            if (incomingMatches && incoming.RetainsPreviousContent)
            {
                return incoming;
            }

            return incomingMatches
                ? incoming
                : CommandProjectionState.Unavailable(
                    generation,
                    "Waiting for a coherent command observation",
                    sessionEpoch: sessionEpoch);
        }

        public static ViewportProjectionState SelectViewport(
            ViewportProjectionState incoming,
            ViewportProjectionState current,
            string sessionEpoch,
            long generation)
        {
            if (!MatchesSession(incoming, sessionEpoch, generation))
            {
                return MatchesSession(current, sessionEpoch, generation)
                    ? current
                    : ViewportProjectionState.Unavailable(
                        "Waiting for viewport",
                        generation,
                        0,
                        sessionEpoch: sessionEpoch);
            }

            if (!MatchesSession(current, sessionEpoch, generation))
            {
                return incoming;
            }

            if (incoming.DemandEpoch < current.DemandEpoch)
            {
                return current;
            }

            if (incoming.DemandEpoch > current.DemandEpoch)
            {
                return incoming;
            }

            var incomingVersion = new ChannelVersion(
                new SessionId(sessionEpoch, generation),
                incoming.Revision);
            var currentVersion = new ChannelVersion(
                new SessionId(current.SessionEpoch, current.SessionGeneration),
                current.Revision);
            if (incomingVersion.IsOlderThan(currentVersion))
            {
                return current;
            }

            return incoming;
        }

        public static OverlayReadModel Compose(
            OverlayReadModel current,
            ProviderChannelState provider)
        {
            var currentState = current ?? OverlayReadModel.Empty("No overlay state collected");
            var semantic = SelectSemantic(
                currentState.Semantic,
                provider != null ? provider.Semantic : null);
            var semanticSnapshot = semantic.Snapshot;
            var commands = SelectCommands(
                semanticSnapshot,
                provider != null ? provider.Commands : null,
                currentState.Commands,
                semantic.SessionEpoch,
                semantic.SessionGeneration);
            var viewport = SelectViewport(
                provider != null ? provider.Viewport : null,
                currentState.Viewport,
                semantic.SessionEpoch,
                semantic.SessionGeneration);
            if (ReferenceEquals(semantic, currentState.Semantic) &&
                ReferenceEquals(commands, currentState.Commands) &&
                ReferenceEquals(viewport, currentState.Viewport))
            {
                return currentState;
            }

            return new OverlayReadModel(semantic, commands, viewport);
        }

        private static bool MatchesSession(
            CommandProjectionState state,
            string sessionEpoch,
            long generation)
        {
            return state != null &&
                   state.SessionGeneration == generation &&
                   string.Equals(state.SessionEpoch, sessionEpoch ?? string.Empty, StringComparison.Ordinal);
        }

        private static bool MatchesSession(
            ViewportProjectionState state,
            string sessionEpoch,
            long generation)
        {
            return state != null &&
                   state.SessionGeneration == generation &&
                   string.Equals(state.SessionEpoch, sessionEpoch ?? string.Empty, StringComparison.Ordinal);
        }
    }
}
