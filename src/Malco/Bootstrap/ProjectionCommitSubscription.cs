using System;
using System.Threading;
using Malco.Application.Contracts.Projection;

namespace Malco.Bootstrap
{
    internal sealed class ProjectionCommitSubscription : IDisposable
    {
        private IProjectionPresentationCommitSource _source;
        private IProjectionPresentationCommitSink _sink;

        public ProjectionCommitSubscription(
            IProjectionPresentationCommitSource source,
            IProjectionPresentationCommitSink sink)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
            source.RegisterProjectionPresentationCommitSink(sink);
        }

        public void Dispose()
        {
            var source = Interlocked.Exchange(ref _source, null);
            var sink = Interlocked.Exchange(ref _sink, null);
            if (source != null && sink != null)
                source.UnregisterProjectionPresentationCommitSink(sink);
        }
    }
}
