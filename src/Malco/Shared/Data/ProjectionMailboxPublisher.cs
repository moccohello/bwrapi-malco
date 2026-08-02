using System.Diagnostics;
using System.Threading;
using Malco.Application.Contracts;
using Malco.Application.Contracts.Projection;
using Malco.Application.Projection;

namespace Malco.Data
{
    internal sealed class ProjectionMailboxPublisher : IProjectionMailboxSource
    {
        private readonly ProjectionMailbox _mailbox = new ProjectionMailbox();
        private readonly object _publishGate = new object();
        private int _closing;
        private long _acquisitionSequence;
        private string _lastSessionEpoch = string.Empty;
        private long _lastGeneration = long.MinValue;
        private long _lastDemandEpoch = long.MinValue;
        private long _lastPresentationRevision = -1;
        private bool _lastMailboxUsable;
        private bool _lastMailboxAuthoritativeClear;
        private ProviderStatus _lastMailboxStatus;
        private IProjectionPresentationCommitSink _presentationCommitSink;

        public IProjectionMailboxReader ProjectionMailboxReader
        {
            get { return _mailbox; }
        }

        public void RegisterProjectionPresentationCommitSink(IProjectionPresentationCommitSink sink)
        {
            if (sink == null) throw new System.ArgumentNullException(nameof(sink));
            lock (_publishGate)
            {
                if (Volatile.Read(ref _closing) == 0) _presentationCommitSink = sink;
            }
        }

        public void UnregisterProjectionPresentationCommitSink(IProjectionPresentationCommitSink sink)
        {
            lock (_publishGate)
            {
                if (ReferenceEquals(_presentationCommitSink, sink)) _presentationCommitSink = null;
            }
        }

        public void Publish(ViewportProjectionState authoritativeState)
        {
            if (authoritativeState == null)
            {
                return;
            }

            lock (_publishGate)
            {
                if (Volatile.Read(ref _closing) != 0)
                {
                    return;
                }

                PublishCoreValues(
                    authoritativeState.SessionEpoch,
                    authoritativeState.SessionGeneration,
                    authoritativeState.DemandEpoch,
                    authoritativeState.Revision,
                    authoritativeState.GameFrame.HasValue,
                    authoritativeState.GameFrame.GetValueOrDefault(),
                    authoritativeState.ViewportMapX,
                    authoritativeState.ViewportMapY,
                    authoritativeState.Status,
                    authoritativeState.IsUsable,
                    authoritativeState.IsAuthoritativeClear,
                    authoritativeState.ClearReason);
            }
        }

        public void BeginClosing()
        {
            lock (_publishGate)
            {
                Volatile.Write(ref _closing, 1);
                _presentationCommitSink = null;
            }
        }

        private void PublishCoreValues(
            string sessionEpoch,
            long generation,
            long demandEpoch,
            long revision,
            bool hasGameFrame,
            int gameFrame,
            int viewportMapX,
            int viewportMapY,
            ProviderStatus status,
            bool usable,
            bool isAuthoritativeClear,
            ProjectionClearReason clearReason)
        {
            if (isAuthoritativeClear != (clearReason != ProjectionClearReason.None))
                throw new System.ArgumentException("Authoritative mailbox clears require one explicit clear reason.");
            var normalizedEpoch = sessionEpoch ?? string.Empty;
            var sameSession = generation == _lastGeneration &&
                              string.Equals(normalizedEpoch, _lastSessionEpoch, System.StringComparison.Ordinal);
            if (sameSession &&
                (demandEpoch < _lastDemandEpoch ||
                 demandEpoch == _lastDemandEpoch && revision < _lastPresentationRevision))
            {
                return;
            }

            var identityChanged = !sameSession ||
                                  demandEpoch != _lastDemandEpoch ||
                                  generation != _lastGeneration ||
                                  revision != _lastPresentationRevision;

            _lastSessionEpoch = normalizedEpoch;
            _lastGeneration = generation;
            _lastDemandEpoch = demandEpoch;
            _lastPresentationRevision = revision;
            var acquisition = ++_acquisitionSequence;
            var authoritativeClear = isAuthoritativeClear && !usable;
            var sample = new ProjectionSample(
                normalizedEpoch,
                generation,
                demandEpoch,
                new AcquisitionSequence(acquisition),
                new ProjectionPresentationRevision(revision),
                hasGameFrame,
                gameFrame,
                Stopwatch.GetTimestamp(),
                usable ? viewportMapX : 0,
                usable ? viewportMapY : 0,
                status,
                usable,
                authoritativeClear,
                authoritativeClear ? clearReason : ProjectionClearReason.None);
            var meaningfulCommit = identityChanged ||
                                   usable != _lastMailboxUsable ||
                                   authoritativeClear != _lastMailboxAuthoritativeClear ||
                                   status != _lastMailboxStatus;
            _mailbox.Publish(sample);
            _lastMailboxUsable = usable;
            _lastMailboxAuthoritativeClear = authoritativeClear;
            _lastMailboxStatus = status;
            if (meaningfulCommit)
            {
                var sink = _presentationCommitSink;
                if (sink != null) sink.MarkProjectionPresentationCommitted();
            }
        }

    }
}
