using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Malco.Models;

namespace Malco.Data
{
    internal enum CommandObservationStatus
    {
        CoherentSelection = 0,
        CoherentDeselection = 1,
        Unavailable = 2,
        Incoherent = 3,
        Error = 4
    }

    internal sealed class SemanticSnapshotState
    {
        private readonly FrozenSemanticSnapshot _snapshot;

        public SemanticSnapshotState(
            ProviderStatus status,
            GameSnapshot snapshot,
            long? sequence,
            int? frame,
            string key,
            long sessionGeneration,
            string message,
            bool isAuthoritativeOutOfMatch = false,
            string sessionEpoch = null)
            : this(
                status,
                FrozenSemanticSnapshot.Freeze(snapshot),
                sequence,
                frame,
                key,
                sessionGeneration,
                message,
                isAuthoritativeOutOfMatch,
                sessionEpoch)
        {
        }

        private SemanticSnapshotState(
            ProviderStatus status,
            FrozenSemanticSnapshot snapshot,
            long? sequence,
            int? frame,
            string key,
            long sessionGeneration,
            string message,
            bool isAuthoritativeOutOfMatch,
            string sessionEpoch)
        {
            Status = status;
            _snapshot = snapshot ?? FrozenSemanticSnapshot.Freeze(GameSnapshotFactory.NotReady(message));
            Sequence = sequence;
            Frame = frame;
            Key = key ?? string.Empty;
            SessionGeneration = sessionGeneration;
            SessionEpoch = sessionEpoch ?? string.Empty;
            Message = message ?? string.Empty;
            IsAuthoritativeOutOfMatch = isAuthoritativeOutOfMatch;
        }

        public ProviderStatus Status { get; }
        public FrozenSemanticSnapshot Snapshot { get { return _snapshot; } }
        public long? Sequence { get; }
        public int? Frame { get; }
        public string Key { get; }
        public long SessionGeneration { get; }
        public string SessionEpoch { get; }
        public string Message { get; }
        public bool IsAuthoritativeOutOfMatch { get; }

        public SemanticSnapshotState WithStatus(
            ProviderStatus status,
            long? sequence,
            int? frame,
            string key,
            string message)
        {
            if (Status == status &&
                Sequence == sequence &&
                Frame == frame &&
                string.Equals(Key, key ?? string.Empty, StringComparison.Ordinal) &&
                string.Equals(Message, message ?? string.Empty, StringComparison.Ordinal))
            {
                return this;
            }

            return new SemanticSnapshotState(
                status,
                _snapshot,
                sequence,
                frame,
                key,
                SessionGeneration,
                message,
                IsAuthoritativeOutOfMatch,
                SessionEpoch);
        }

        public static SemanticSnapshotState Empty(string message)
        {
            return new SemanticSnapshotState(
                ProviderStatus.NotReady,
                GameSnapshotFactory.NotReady(message),
                null,
                null,
                string.Empty,
                0,
                message,
                isAuthoritativeOutOfMatch: false,
                sessionEpoch: string.Empty);
        }
    }

    internal sealed class CommandProjectionState
    {
        private readonly ReadOnlyCollection<SpatialLine> _lines;
        private readonly ReadOnlyCollection<string> _selectedTags;
        private readonly string[] _selectedUnitTags;

        public CommandProjectionState(
            CommandObservationStatus status,
            IEnumerable<SpatialLine> lines,
            IEnumerable<string> selectedUnitTags,
            long? sequence,
            int? frame,
            string key,
            long? baseSemanticSequence,
            long sessionGeneration,
            string message,
            long revision = 0,
            SelectionCompleteness selectionCompleteness = SelectionCompleteness.Unknown,
            long demandEpoch = 0,
            bool isDemanded = true,
            bool isAuthoritativeClear = false,
            string sessionEpoch = null,
            bool retainsPreviousContent = false,
            ProjectionClearReason clearReason = ProjectionClearReason.None)
        {
            if (isAuthoritativeClear != (clearReason != ProjectionClearReason.None))
                throw new ArgumentException("Authoritative command clears require one explicit clear reason.");
            Status = status;
            _lines = Array.AsReadOnly((lines ?? Enumerable.Empty<SpatialLine>()).Where(line => line != null).ToArray());
            _selectedUnitTags = (selectedUnitTags ?? Enumerable.Empty<string>())
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            _selectedTags = Array.AsReadOnly(_selectedUnitTags);
            Sequence = sequence;
            Frame = frame;
            Key = key ?? string.Empty;
            BaseSemanticSequence = baseSemanticSequence;
            SessionGeneration = sessionGeneration;
            SessionEpoch = sessionEpoch ?? string.Empty;
            Message = message ?? string.Empty;
            Revision = revision;
            SelectionCompleteness = selectionCompleteness;
            DemandEpoch = demandEpoch;
            IsDemanded = isDemanded;
            IsAuthoritativeClear = isAuthoritativeClear;
            ClearReason = clearReason;
            RetainsPreviousContent = retainsPreviousContent;
        }

        public CommandObservationStatus Status { get; }
        public IReadOnlyList<SpatialLine> Lines { get { return _lines; } }
        public IReadOnlyList<string> SelectedUnitTags { get { return _selectedTags; } }
        public long? Sequence { get; }
        public int? Frame { get; }
        public string Key { get; }
        public long? BaseSemanticSequence { get; }
        public long SessionGeneration { get; }
        public string SessionEpoch { get; }
        public string Message { get; }
        public long Revision { get; }
        public SelectionCompleteness SelectionCompleteness { get; }
        public long DemandEpoch { get; }
        public bool IsDemanded { get; }
        public bool IsAuthoritativeClear { get; }
        public ProjectionClearReason ClearReason { get; }
        public bool RetainsPreviousContent { get; }
        public bool IsCoherent { get { return Status == CommandObservationStatus.CoherentSelection || Status == CommandObservationStatus.CoherentDeselection; } }

        public static CommandProjectionState Empty(
            long? sequence,
            int? frame,
            long? baseSemanticSequence,
            long sessionGeneration,
            string message,
            long revision = 0,
            SelectionCompleteness selectionCompleteness = SelectionCompleteness.Authoritative,
            long demandEpoch = 0,
            bool isDemanded = true,
            bool isAuthoritativeClear = false,
            string sessionEpoch = null,
            ProjectionClearReason clearReason = ProjectionClearReason.None)
        {
            return new CommandProjectionState(
                CommandObservationStatus.CoherentDeselection,
                null,
                null,
                sequence,
                frame,
                "deselected",
                baseSemanticSequence,
                sessionGeneration,
                message,
                revision,
                selectionCompleteness,
                demandEpoch,
                isDemanded,
                isAuthoritativeClear,
                sessionEpoch,
                clearReason: clearReason);
        }

        public static CommandProjectionState Unavailable(
            long sessionGeneration,
            string message,
            long revision = 0,
            SelectionCompleteness selectionCompleteness = SelectionCompleteness.Unknown,
            long demandEpoch = 0,
            bool isDemanded = true,
            bool isAuthoritativeClear = false,
            string sessionEpoch = null,
            bool retainsPreviousContent = false,
            ProjectionClearReason clearReason = ProjectionClearReason.None)
        {
            return new CommandProjectionState(
                CommandObservationStatus.Unavailable,
                null,
                null,
                null,
                null,
                string.Empty,
                null,
                sessionGeneration,
                message,
                revision,
                selectionCompleteness,
                demandEpoch,
                isDemanded,
                isAuthoritativeClear,
                sessionEpoch,
                retainsPreviousContent,
                clearReason);
        }

    }

    internal sealed class OverlayReadModel
    {
        public OverlayReadModel(
            SemanticSnapshotState semantic,
            CommandProjectionState commands,
            ViewportProjectionState viewport)
        {
            Semantic = semantic ?? SemanticSnapshotState.Empty("No semantic state collected");
            Commands = commands ?? CommandProjectionState.Unavailable(
                Semantic.SessionGeneration,
                "No command projection collected",
                sessionEpoch: Semantic.SessionEpoch);
            Viewport = viewport ?? ViewportProjectionState.Unavailable(
                "No viewport projection collected",
                Semantic.SessionGeneration,
                sessionEpoch: Semantic.SessionEpoch);
        }

        public SemanticSnapshotState Semantic { get; }
        public CommandProjectionState Commands { get; }
        public ViewportProjectionState Viewport { get; }

        public static OverlayReadModel Empty(string message)
        {
            var semantic = SemanticSnapshotState.Empty(message);
            return new OverlayReadModel(
                semantic,
                CommandProjectionState.Unavailable(
                    semantic.SessionGeneration,
                    message,
                    sessionEpoch: semantic.SessionEpoch),
                ViewportProjectionState.Unavailable(
                    message,
                    semantic.SessionGeneration,
                    sessionEpoch: semantic.SessionEpoch));
        }
    }
}
