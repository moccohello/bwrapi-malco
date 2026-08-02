using System;
using System.Threading;
using Malco.Application.Contracts.Projection;
using Malco.Models;

namespace Malco.Data
{
    internal sealed partial class ProviderPublicationCoordinator
    {
        private const long PendingProjectionRevision = -1;

        private readonly object _gate = new object();
        private readonly EmbeddedObserverMetrics _metrics;
        private readonly GameSnapshotMapper _snapshotMapper = new GameSnapshotMapper();
        private readonly BwrApiSemanticRuntimeMapper _semanticRuntimeMapper =
            new BwrApiSemanticRuntimeMapper();
        private readonly ProjectionMailboxPublisher _projectionMailbox =
            new ProjectionMailboxPublisher();

        private ProviderChannelState _channels;
        private IProviderCommitSink _commitSink;
        private int _closing;
        private bool _terminalPublication;
        private long _semanticGeneration;
        private string _semanticEpoch = string.Empty;

        public ProviderPublicationCoordinator(EmbeddedObserverMetrics metrics)
        {
            _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
            var snapshot = GameSnapshotFactory.NotReady(
                "Waiting for tracked StarCraft process");
            var semantic = new SemanticSnapshotState(
                ProviderStatus.NotReady,
                snapshot,
                null,
                null,
                string.Empty,
                0,
                snapshot.WorkerStateStatus);
            _channels = new ProviderChannelState(
                semantic,
                CommandProjectionState.Unavailable(0, snapshot.WorkerStateStatus),
                ViewportProjectionState.Unavailable(snapshot.WorkerStateStatus));
            _projectionMailbox.Publish(_channels.Viewport);
        }

        public bool IsClosing => Volatile.Read(ref _closing) != 0;

        public IProjectionMailboxReader ProjectionMailboxReader =>
            _projectionMailbox.ProjectionMailboxReader;

        public ProviderChannelState GetProviderChannelState() =>
            Volatile.Read(ref _channels);

        public void RegisterCommitSink(IProviderCommitSink sink)
        {
            if (sink == null) throw new ArgumentNullException(nameof(sink));
            lock (_gate)
            {
                if (IsClosing || _terminalPublication) return;
                _commitSink = sink;
                sink.MarkProviderCommit(
                    ProviderCommitMask.Semantic |
                    ProviderCommitMask.Commands |
                    ProviderCommitMask.ProjectionControl);
            }
        }

        public void UnregisterCommitSink(IProviderCommitSink sink)
        {
            lock (_gate)
            {
                if (ReferenceEquals(_commitSink, sink)) _commitSink = null;
            }
        }

        public void RegisterProjectionPresentationCommitSink(
            IProjectionPresentationCommitSink sink) =>
            _projectionMailbox.RegisterProjectionPresentationCommitSink(sink);

        public void UnregisterProjectionPresentationCommitSink(
            IProjectionPresentationCommitSink sink) =>
            _projectionMailbox.UnregisterProjectionPresentationCommitSink(sink);

        public bool TryBeginClosing()
        {
            return Interlocked.Exchange(ref _closing, 1) == 0;
        }

        public void CompleteClosing()
        {
            lock (_gate)
            {
                _commitSink = null;
            }

            _projectionMailbox.BeginClosing();
        }

    }
}
