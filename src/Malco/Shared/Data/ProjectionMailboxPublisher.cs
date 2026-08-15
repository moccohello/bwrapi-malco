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

        public void Commit(
            ViewportProjectionState authoritativeState,
            out IProjectionPresentationCommitSink commitSink)
        {
            commitSink = null;
            if (authoritativeState == null) return;

            lock (_publishGate)
            {
                if (Volatile.Read(ref _closing) != 0) return;
                if (authoritativeState.IsAuthoritativeClear !=
                    (authoritativeState.ClearReason != ProjectionClearReason.None))
                    throw new System.ArgumentException("Authoritative mailbox clears require one explicit clear reason.");
                var sessionEpoch = authoritativeState.SessionEpoch ?? string.Empty;
                var sameSession = authoritativeState.SessionGeneration == _lastGeneration &&
                                  string.Equals(sessionEpoch, _lastSessionEpoch, System.StringComparison.Ordinal);
                if (sameSession &&
                    (authoritativeState.DemandEpoch < _lastDemandEpoch ||
                     authoritativeState.DemandEpoch == _lastDemandEpoch &&
                     authoritativeState.Revision < _lastPresentationRevision)) return;

                var meaningfulCommit = !sameSession ||
                    authoritativeState.DemandEpoch != _lastDemandEpoch ||
                    authoritativeState.Revision != _lastPresentationRevision;
                _lastSessionEpoch = sessionEpoch;
                _lastGeneration = authoritativeState.SessionGeneration;
                _lastDemandEpoch = authoritativeState.DemandEpoch;
                _lastPresentationRevision = authoritativeState.Revision;
                var authoritativeClear = authoritativeState.IsAuthoritativeClear &&
                                         !authoritativeState.IsUsable;
                meaningfulCommit |= authoritativeState.IsUsable != _lastMailboxUsable ||
                                    authoritativeClear != _lastMailboxAuthoritativeClear ||
                                    authoritativeState.Status != _lastMailboxStatus;
                _mailbox.Publish(new ProjectionSample(
                    sessionEpoch,
                    authoritativeState.SessionGeneration,
                    authoritativeState.DemandEpoch,
                    new AcquisitionSequence(++_acquisitionSequence),
                    new ProjectionPresentationRevision(authoritativeState.Revision),
                    authoritativeState.GameFrame.HasValue,
                    authoritativeState.GameFrame.GetValueOrDefault(),
                    Stopwatch.GetTimestamp(),
                    authoritativeState.IsUsable ? authoritativeState.ViewportMapX : 0,
                    authoritativeState.IsUsable ? authoritativeState.ViewportMapY : 0,
                    authoritativeState.Status,
                    authoritativeState.IsUsable,
                    authoritativeClear,
                    authoritativeClear ? authoritativeState.ClearReason : ProjectionClearReason.None));
                _lastMailboxUsable = authoritativeState.IsUsable;
                _lastMailboxAuthoritativeClear = authoritativeClear;
                _lastMailboxStatus = authoritativeState.Status;
                if (meaningfulCommit) commitSink = _presentationCommitSink;
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

    }
}
