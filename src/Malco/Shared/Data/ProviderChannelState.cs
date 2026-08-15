using System;
using System.Threading;
using Malco.Application.Contracts;

namespace Malco.Data
{
    internal sealed class ProviderChannelState
    {
        public ProviderChannelState(
            SemanticSnapshotState semantic,
            CommandProjectionState commands,
            ViewportProjectionState viewport,
            DateTime? semanticPublishedAtUtc = null)
        {
            Semantic = semantic ?? SemanticSnapshotState.Empty("No semantic state collected");
            Commands = commands ?? CommandProjectionState.Unavailable(
                Semantic.SessionGeneration,
                "No command projection collected",
                sessionEpoch: Semantic.SessionEpoch);
            Viewport = viewport ?? ViewportProjectionState.Unavailable(
                "No viewport projection collected",
                Semantic.SessionGeneration,
                0,
                sessionEpoch: Semantic.SessionEpoch);
            SemanticPublishedAtUtc = semanticPublishedAtUtc ?? DateTime.UtcNow;
        }

        public SemanticSnapshotState Semantic { get; }
        public CommandProjectionState Commands { get; }
        public ViewportProjectionState Viewport { get; }
        public DateTime SemanticPublishedAtUtc { get; }

        public static ProviderChannelState Empty(string message)
        {
            var semantic = SemanticSnapshotState.Empty(message);
            return new ProviderChannelState(
                semantic,
                CommandProjectionState.Unavailable(
                    semantic.SessionGeneration,
                    message,
                    sessionEpoch: semantic.SessionEpoch),
                ViewportProjectionState.Unavailable(
                    message,
                    semantic.SessionGeneration,
                    0,
                    sessionEpoch: semantic.SessionEpoch));
        }
    }

    internal interface IProviderChannelStateProvider
    {
        ProviderChannelState GetProviderChannelState();
    }

    internal static class ProviderChannelStateUpdater
    {
        public static ProviderChannelState UpdateSemantic(
            ref ProviderChannelState location,
            SemanticSnapshotState semantic)
        {
            return UpdateSemanticCore(ref location, semantic);
        }

        public static ProviderChannelState UpdateSemanticStatus(
            ref ProviderChannelState location,
            SemanticSnapshotState semantic)
        {
            return UpdateSemanticCore(ref location, semantic);
        }

        private static ProviderChannelState UpdateSemanticCore(
            ref ProviderChannelState location,
            SemanticSnapshotState semantic)
        {
            if (semantic == null)
            {
                return Volatile.Read(ref location);
            }

            while (true)
            {
                var current = Volatile.Read(ref location) ?? ProviderChannelState.Empty(semantic.Message);
                if (ReferenceEquals(current.Semantic, semantic))
                {
                    return current;
                }
                var sessionChanged = current.Semantic == null ||
                                     current.Semantic.SessionGeneration != semantic.SessionGeneration ||
                                     !string.Equals(
                                         current.Semantic.SessionEpoch,
                                         semantic.SessionEpoch,
                                         StringComparison.Ordinal);
                var becameAuthoritativelyOutOfMatch = semantic.IsAuthoritativeOutOfMatch &&
                                                      (current.Semantic == null ||
                                                       !current.Semantic.IsAuthoritativeOutOfMatch);
                var resetChannels = sessionChanged || becameAuthoritativelyOutOfMatch;
                var resetMessage = becameAuthoritativelyOutOfMatch
                    ? "No active match"
                    : "Waiting for a coherent observation in the new match";
                var commands = resetChannels
                    ? CommandProjectionState.Unavailable(
                        semantic.SessionGeneration,
                        resetMessage,
                        sessionChanged ? -1 : (current.Commands?.Revision ?? 0) + 1,
                        SelectionCompleteness.Unknown,
                        current.Commands != null ? current.Commands.DemandEpoch : 0,
                        current.Commands == null || current.Commands.IsDemanded,
                        true,
                        semantic.SessionEpoch,
                        clearReason: sessionChanged
                            ? ProjectionClearReason.SessionGenerationChanged
                            : ProjectionClearReason.OutOfMatch)
                    : current.Commands;
                var viewport = resetChannels
                    ? ViewportProjectionState.Unavailable(
                        resetMessage,
                        semantic.SessionGeneration,
                        sessionChanged ? -1 : (current.Viewport?.Revision ?? 0) + 1,
                        current.Viewport != null ? current.Viewport.DemandEpoch : 0,
                        ProviderStatus.NotReady,
                        semantic.SessionEpoch,
                        true,
                        sessionChanged
                            ? ProjectionClearReason.SessionGenerationChanged
                            : ProjectionClearReason.OutOfMatch)
                    : current.Viewport;
                var next = new ProviderChannelState(
                    semantic,
                    commands,
                    viewport,
                    DateTime.UtcNow);
                if (ReferenceEquals(Interlocked.CompareExchange(ref location, next, current), current))
                {
                    return next;
                }
            }
        }

        public static ProviderChannelState UpdateCommands(
            ref ProviderChannelState location,
            CommandProjectionState commands)
        {
            if (commands == null)
            {
                return Volatile.Read(ref location);
            }

            while (true)
            {
                var current = Volatile.Read(ref location);
                var next = SelectCommands(current, commands);
                if (ReferenceEquals(next, current))
                {
                    return current;
                }

                if (ReferenceEquals(Interlocked.CompareExchange(ref location, next, current), current))
                {
                    return next;
                }
            }
        }

        internal static ProviderChannelState SelectCommands(
            ProviderChannelState current,
            CommandProjectionState commands)
        {
            if (commands == null ||
                current == null ||
                current.Semantic == null ||
                commands.SessionGeneration != current.Semantic.SessionGeneration ||
                !string.Equals(
                    commands.SessionEpoch,
                    current.Semantic.SessionEpoch,
                    StringComparison.Ordinal) ||
                ReferenceEquals(current.Commands, commands))
            {
                return current;
            }

            var currentCommandVersion = current.Commands == null
                ? default(ChannelVersion)
                : new ChannelVersion(
                    new SessionId(current.Commands.SessionEpoch, current.Commands.SessionGeneration),
                    current.Commands.Revision);
            var incomingCommandVersion = new ChannelVersion(
                new SessionId(commands.SessionEpoch, commands.SessionGeneration),
                commands.Revision);
            if (current.Commands != null &&
                (current.Commands.DemandEpoch > commands.DemandEpoch ||
                 (current.Commands.DemandEpoch == commands.DemandEpoch &&
                  currentCommandVersion.IsAtLeast(incomingCommandVersion))))
            {
                return current;
            }

            return new ProviderChannelState(
                current.Semantic,
                commands,
                current.Viewport,
                current.SemanticPublishedAtUtc);
        }

        public static ProviderChannelState UpdateViewport(
            ref ProviderChannelState location,
            ViewportProjectionState viewport)
        {
            if (viewport == null)
            {
                return Volatile.Read(ref location);
            }

            while (true)
            {
                var current = Volatile.Read(ref location);
                var next = SelectViewport(current, viewport);
                if (ReferenceEquals(next, current))
                {
                    return current;
                }

                if (ReferenceEquals(Interlocked.CompareExchange(ref location, next, current), current))
                {
                    return next;
                }
            }
        }

        internal static ProviderChannelState SelectViewport(
            ProviderChannelState current,
            ViewportProjectionState viewport)
        {
            if (viewport == null ||
                current == null ||
                current.Semantic == null ||
                viewport.SessionGeneration != current.Semantic.SessionGeneration ||
                !string.Equals(
                    viewport.SessionEpoch,
                    current.Semantic.SessionEpoch,
                    StringComparison.Ordinal))
            {
                return current;
            }

            var currentViewportVersion = current.Viewport == null
                ? default(ChannelVersion)
                : new ChannelVersion(
                    new SessionId(current.Viewport.SessionEpoch, current.Viewport.SessionGeneration),
                    current.Viewport.Revision);
            var incomingViewportVersion = new ChannelVersion(
                new SessionId(viewport.SessionEpoch, viewport.SessionGeneration),
                viewport.Revision);
            if (current.Viewport != null &&
                (current.Viewport.DemandEpoch > viewport.DemandEpoch ||
                 (current.Viewport.DemandEpoch == viewport.DemandEpoch &&
                  currentViewportVersion.IsAtLeast(incomingViewportVersion))))
            {
                return current;
            }

            if (ReferenceEquals(current.Viewport, viewport))
            {
                return current;
            }

            return new ProviderChannelState(
                current.Semantic,
                current.Commands,
                viewport,
                current.SemanticPublishedAtUtc);
        }
    }
}
