using System;
using System.Linq;
using BwrApi.Client;

namespace Malco.Data
{
    internal static class ProviderProjectionMapper
    {
        public static ViewportProjectionState MapViewport(
            BwrApiViewportProjectionV1 source)
        {
            long generation = checked((long)source.SessionGeneration);
            long revision = checked((long)source.Revision);
            long demandEpoch = checked((long)source.DemandEpoch);
            ProjectionClearReason clearReason =
                ConvertClearReason(source.ClearReason);
            ValidateProjectionClear(
                source.IsAuthoritativeClear,
                clearReason,
                "viewport");
            if (source.Status == BwrApiViewportProjectionStatus.Ready &&
                source.ViewportMapPosition.HasValue)
            {
                if (source.IsAuthoritativeClear)
                {
                    throw new BwrApiNativeArtifactException(
                        "viewport_projection_clear_invalid",
                        "A usable viewport projection cannot also be an authoritative clear.");
                }

                return ViewportProjectionState.Ready(
                    source.ViewportMapPosition.Value.X,
                    source.ViewportMapPosition.Value.Y,
                    source.RuntimeSequence.HasValue
                        ? checked((long)source.RuntimeSequence.Value)
                        : (long?)null,
                    source.GameFrame.HasValue
                        ? checked((int)source.GameFrame.Value)
                        : (int?)null,
                    source.Message ?? "Embedded viewport ready",
                    generation,
                    revision,
                    demandEpoch,
                    source.SessionEpoch);
            }

            return ViewportProjectionState.Unavailable(
                source.Message ?? "Embedded viewport unavailable",
                generation,
                revision,
                demandEpoch,
                source.Status switch
                {
                    BwrApiViewportProjectionStatus.Stale =>
                        ProviderStatus.Stale,
                    BwrApiViewportProjectionStatus.Error =>
                        ProviderStatus.Error,
                    _ => ProviderStatus.NotReady
                },
                source.SessionEpoch,
                source.IsAuthoritativeClear,
                clearReason);
        }

        public static CommandProjectionState MapCommand(
            BwrApiSelectedCommandProjectionV1 source,
            Func<CommandProjectionState> readRetainedCandidate)
        {
            ProjectionClearReason clearReason =
                ConvertClearReason(source.ClearReason);
            ValidateProjectionClear(
                source.IsAuthoritativeClear,
                clearReason,
                "selected-command");
            SpatialLine[] lines = source.Lines.Select(line => new SpatialLine(
                SpatialLineIdentity.Create(
                    line.SourceUnitTag,
                    KindName(line.Kind),
                    line.Sequence),
                line.SourceUnitTypeId,
                BwapiBroodWarTables.GetUnitTypeInfo(
                    line.SourceUnitTypeId).Name,
                KindName(line.Kind),
                line.Sequence,
                line.SourceMapPosition.X,
                line.SourceMapPosition.Y,
                line.TargetMapPosition.X,
                line.TargetMapPosition.Y)).ToArray();
            CommandProjectionState retainedCandidate =
                source.RetainedCoherentRevision.HasValue
                    ? readRetainedCandidate()
                    : null;
            var canRetain =
                retainedCandidate != null &&
                source.RetainedCoherentRevision.HasValue &&
                retainedCandidate.Revision ==
                    checked((long)source.RetainedCoherentRevision.Value) &&
                retainedCandidate.DemandEpoch ==
                    checked((long)source.DemandEpoch) &&
                retainedCandidate.SessionGeneration ==
                    checked((long)source.SessionGeneration) &&
                string.Equals(
                    retainedCandidate.SessionEpoch,
                    source.SessionEpoch,
                    StringComparison.Ordinal);
            return new CommandProjectionState(
                ConvertCommandStatus(source.Status),
                canRetain ? retainedCandidate.Lines : lines,
                canRetain
                    ? retainedCandidate.SelectedUnitTags
                    : source.SelectedUnitTags,
                canRetain
                    ? retainedCandidate.Sequence
                    : source.RuntimeSequence.HasValue
                        ? checked((long)source.RuntimeSequence.Value)
                        : (long?)null,
                canRetain
                    ? retainedCandidate.Frame
                    : source.GameFrame.HasValue
                        ? checked((int)source.GameFrame.Value)
                        : (int?)null,
                canRetain
                    ? retainedCandidate.Key
                    : SpatialLineIdentity.BuildContentKey(lines),
                canRetain
                    ? retainedCandidate.BaseSemanticSequence
                    : source.BaseSemanticSequence.HasValue
                        ? checked((long)source.BaseSemanticSequence.Value)
                        : (long?)null,
                checked((long)source.SessionGeneration),
                source.Message ?? string.Empty,
                checked((long)source.Revision),
                ConvertCompleteness(source.SelectionCompleteness),
                checked((long)source.DemandEpoch),
                source.IsDemanded,
                source.IsAuthoritativeClear,
                source.SessionEpoch,
                canRetain,
                clearReason: clearReason);
        }

        private static ProjectionClearReason ConvertClearReason(
            BwrApiObserverProjectionClearReason reason) => reason switch
        {
            BwrApiObserverProjectionClearReason.None =>
                ProjectionClearReason.None,
            BwrApiObserverProjectionClearReason.OutOfMatch =>
                ProjectionClearReason.OutOfMatch,
            BwrApiObserverProjectionClearReason.SessionGenerationChanged =>
                ProjectionClearReason.SessionGenerationChanged,
            BwrApiObserverProjectionClearReason.DemandChanged =>
                ProjectionClearReason.DemandChanged,
            _ => throw new BwrApiNativeArtifactException(
                "projection_clear_reason_invalid",
                "The embedded SDK returned an unknown projection clear reason.")
        };

        private static void ValidateProjectionClear(
            bool isAuthoritativeClear,
            ProjectionClearReason clearReason,
            string channel)
        {
            if (isAuthoritativeClear !=
                (clearReason != ProjectionClearReason.None))
            {
                throw new BwrApiNativeArtifactException(
                    channel + "_projection_clear_invalid",
                    "The embedded SDK returned a non-canonical authoritative clear tuple.");
            }
        }

        private static CommandObservationStatus ConvertCommandStatus(
            BwrApiSelectedCommandProjectionStatus status) => status switch
        {
            BwrApiSelectedCommandProjectionStatus.CoherentSelection =>
                CommandObservationStatus.CoherentSelection,
            BwrApiSelectedCommandProjectionStatus.CoherentDeselection =>
                CommandObservationStatus.CoherentDeselection,
            BwrApiSelectedCommandProjectionStatus.Incoherent =>
                CommandObservationStatus.Incoherent,
            _ => CommandObservationStatus.Unavailable
        };

        private static SelectionCompleteness ConvertCompleteness(
            BwrApiSelectionCompleteness completeness) => completeness switch
        {
            BwrApiSelectionCompleteness.Authoritative =>
                SelectionCompleteness.Authoritative,
            _ => SelectionCompleteness.Unknown
        };

        private static string KindName(
            BwrApiSelectedCommandLineKind kind) => kind switch
        {
            BwrApiSelectedCommandLineKind.MoveQueued => "move-queued",
            BwrApiSelectedCommandLineKind.AttackQueued => "attack-queued",
            BwrApiSelectedCommandLineKind.PatrolQueued => "patrol-queued",
            BwrApiSelectedCommandLineKind.ResourceQueued =>
                "resource-queued",
            BwrApiSelectedCommandLineKind.SpellQueued => "spell-queued",
            BwrApiSelectedCommandLineKind.BuildQueued => "build-queued",
            _ => kind.ToString().ToLowerInvariant()
        };
    }
}
