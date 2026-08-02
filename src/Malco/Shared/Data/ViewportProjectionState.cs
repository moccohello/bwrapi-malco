using System;

namespace Malco.Data
{
    internal enum ProjectionClearReason
    {
        None = 0,
        OutOfMatch = 1,
        SessionGenerationChanged = 2,
        DemandChanged = 3,
        SourceReset = 4
    }

    internal sealed class ViewportProjectionState
    {
        private ViewportProjectionState(
            bool hasViewport,
            int viewportMapX,
            int viewportMapY,
            DateTime capturedAtUtc,
            long? runtimeSequence,
            int? gameFrame,
            ProviderStatus status,
            string message,
            long sessionGeneration,
            long revision,
            long demandEpoch,
            string sessionEpoch,
            bool isAuthoritativeClear,
            ProjectionClearReason clearReason)
        {
            HasViewport = hasViewport;
            ViewportMapX = viewportMapX;
            ViewportMapY = viewportMapY;
            CapturedAtUtc = capturedAtUtc;
            RuntimeSequence = runtimeSequence;
            GameFrame = gameFrame;
            Status = status;
            Message = message ?? string.Empty;
            SessionGeneration = sessionGeneration;
            SessionEpoch = sessionEpoch ?? string.Empty;
            Revision = revision;
            DemandEpoch = demandEpoch;
            IsAuthoritativeClear = isAuthoritativeClear;
            ClearReason = clearReason;
        }

        public bool HasViewport { get; }

        public int ViewportMapX { get; }

        public int ViewportMapY { get; }

        public DateTime CapturedAtUtc { get; }

        public long? RuntimeSequence { get; }

        public int? GameFrame { get; }

        public ProviderStatus Status { get; }

        public string Message { get; }

        public long SessionGeneration { get; }

        public string SessionEpoch { get; }

        public long Revision { get; }

        public long DemandEpoch { get; }

        public bool IsAuthoritativeClear { get; }

        public ProjectionClearReason ClearReason { get; }

        public bool IsUsable
        {
            get { return Status == ProviderStatus.Ready && HasViewport; }
        }

        public static ViewportProjectionState Ready(
            int viewportMapX,
            int viewportMapY,
            long? runtimeSequence,
            int? gameFrame,
            string message,
            long sessionGeneration = 0,
            long revision = 0,
            long demandEpoch = 0,
            string sessionEpoch = null)
        {
            return new ViewportProjectionState(
                true,
                viewportMapX,
                viewportMapY,
                DateTime.UtcNow,
                runtimeSequence,
                gameFrame,
                ProviderStatus.Ready,
                message,
                sessionGeneration,
                revision,
                demandEpoch,
                sessionEpoch,
                false,
                ProjectionClearReason.None);
        }

        public static ViewportProjectionState Unavailable(
            string message,
            long sessionGeneration = 0,
            long revision = 0,
            long demandEpoch = 0,
            ProviderStatus status = ProviderStatus.NotReady,
            string sessionEpoch = null,
            bool isAuthoritativeClear = false,
            ProjectionClearReason clearReason = ProjectionClearReason.None)
        {
            if (status == ProviderStatus.Ready) status = ProviderStatus.NotReady;
            if (isAuthoritativeClear != (clearReason != ProjectionClearReason.None))
                throw new ArgumentException("Authoritative viewport clears require one explicit clear reason.");
            return new ViewportProjectionState(
                false,
                0,
                0,
                DateTime.UtcNow,
                null,
                null,
                status,
                message,
                sessionGeneration,
                revision,
                demandEpoch,
                sessionEpoch,
                isAuthoritativeClear,
                clearReason);
        }
    }
}
