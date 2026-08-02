using System;

namespace Malco.Data
{
    [Flags]
    internal enum ProviderCommitMask
    {
        None = 0,
        Semantic = 1,
        Commands = 2,
        ProjectionControl = 4
    }

    internal interface IProviderCommitSink
    {
        void MarkProviderCommit(ProviderCommitMask mask);
    }

    internal interface IProviderCommitSignalSource
    {
        void RegisterCommitSink(IProviderCommitSink sink);

        void UnregisterCommitSink(IProviderCommitSink sink);
    }
}
