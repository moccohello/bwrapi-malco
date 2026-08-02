using Malco.Data;

namespace Malco.Application.Contracts.Projection
{
    internal readonly struct ProjectionSample
    {
        public ProjectionSample(
            string sessionEpoch,
            long sessionGeneration,
            long demandEpoch,
            AcquisitionSequence acquisitionSequence,
            ProjectionPresentationRevision presentationRevision,
            bool hasGameFrame,
            int gameFrame,
            long capturedAtMonotonicTicks,
            int viewportMapX,
            int viewportMapY,
            ProviderStatus status,
            bool isUsable,
            bool isAuthoritativeClear,
            ProjectionClearReason clearReason)
        {
            SessionEpoch = sessionEpoch ?? string.Empty;
            SessionGeneration = sessionGeneration;
            DemandEpoch = demandEpoch;
            AcquisitionSequence = acquisitionSequence;
            ProjectionPresentationRevision = presentationRevision;
            HasGameFrame = hasGameFrame;
            GameFrame = gameFrame;
            CapturedAtMonotonicTicks = capturedAtMonotonicTicks;
            ViewportMapX = viewportMapX;
            ViewportMapY = viewportMapY;
            Status = status;
            IsUsable = isUsable;
            IsAuthoritativeClear = isAuthoritativeClear;
            ClearReason = clearReason;
        }

        public string SessionEpoch { get; }

        public long SessionGeneration { get; }

        public long DemandEpoch { get; }

        public AcquisitionSequence AcquisitionSequence { get; }

        public ProjectionPresentationRevision ProjectionPresentationRevision { get; }

        public bool HasGameFrame { get; }

        public int GameFrame { get; }

        public long CapturedAtMonotonicTicks { get; }

        public int ViewportMapX { get; }

        public int ViewportMapY { get; }

        public ProviderStatus Status { get; }

        public bool IsUsable { get; }

        public bool IsAuthoritativeClear { get; }

        public ProjectionClearReason ClearReason { get; }
    }

    internal interface IProjectionMailboxReader
    {
        bool TryReadLatest(out ProjectionSample sample);
    }

    internal interface IProjectionPresentationCommitSink
    {
        void MarkProjectionPresentationCommitted();
    }

    internal interface IProjectionPresentationCommitSource
    {
        void RegisterProjectionPresentationCommitSink(IProjectionPresentationCommitSink sink);
        void UnregisterProjectionPresentationCommitSink(IProjectionPresentationCommitSink sink);
    }

    internal interface IProjectionMailboxSource : IProjectionPresentationCommitSource
    {
        IProjectionMailboxReader ProjectionMailboxReader { get; }
    }
}
