using Malco.Data;

namespace Malco.Application.Contracts.Input
{
    internal readonly struct ProjectionControlState
    {
        public ProjectionControlState(
            ProviderStatus status,
            string sessionEpoch,
            long sessionGeneration,
            long demandEpoch,
            bool isDemanded,
            bool isAuthoritativeClear,
            ProjectionClearReason clearReason,
            ContentRevision projectionRevision,
            string message)
        {
            Status = status;
            SessionEpoch = sessionEpoch ?? string.Empty;
            SessionGeneration = sessionGeneration;
            DemandEpoch = demandEpoch;
            IsDemanded = isDemanded;
            IsAuthoritativeClear = isAuthoritativeClear;
            ClearReason = clearReason;
            ProjectionRevision = projectionRevision;
            Message = message ?? string.Empty;
        }

        public ProviderStatus Status { get; }

        public string SessionEpoch { get; }

        public long SessionGeneration { get; }

        public long DemandEpoch { get; }

        public bool IsDemanded { get; }

        public bool IsAuthoritativeClear { get; }

        public ProjectionClearReason ClearReason { get; }

        public ContentRevision ProjectionRevision { get; }

        public string Message { get; }
    }
}
