using Malco.Data;

namespace Malco.Application.Contracts.Output
{
    internal interface IOverlayReadModelSource
    {
        OverlayReadModel Latest { get; }
    }

    internal interface IOverlayStateCommitSink
    {
        void MarkOverlayStateCommitted(OverlayReadModel state);
    }

    internal interface IOverlayStateCommitSource
    {
        void RegisterStateCommitSink(IOverlayStateCommitSink sink);

        void UnregisterStateCommitSink(IOverlayStateCommitSink sink);
    }
}
