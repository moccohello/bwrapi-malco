using System;
using System.Windows.Threading;
using Malco.Game.Services;
using Malco.Integration.Telemetry;
using Malco.Presentation.Scheduling;
using Malco.Shell;
using Malco.Shell.Shutdown;

namespace Malco.Bootstrap
{
    internal sealed class OverlayRuntimeSessionHost : IDisposable
    {
        private readonly GameCoordinator _coordinator;
        private readonly MalcoTelemetryIntegration _telemetry;
        private readonly DispatcherPresentationScheduler _presentationScheduler;
        private readonly DispatcherTimer _presentationClock;
        private readonly ProjectionCommitSubscription _projectionCommitSubscription;
        private readonly CompositionFramePump _framePump;
        private readonly OverlayShellController _shellController;
        private readonly OverlayShutdownController _shutdownController;
        private readonly OverlayApplicationSession _applicationSession;
        private readonly Action _hideOverlay;
        private bool _started;
        private bool _shutdownPrepared;
        private bool _completed;

        public OverlayRuntimeSessionHost(
            GameCoordinator coordinator,
            MalcoTelemetryIntegration telemetry,
            DispatcherPresentationScheduler presentationScheduler,
            DispatcherTimer presentationClock,
            ProjectionCommitSubscription projectionCommitSubscription,
            CompositionFramePump framePump,
            OverlayShellController shellController,
            OverlayShutdownController shutdownController,
            OverlayApplicationSession applicationSession,
            Action hideOverlay)
        {
            _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            _telemetry = telemetry;
            _presentationScheduler = presentationScheduler ?? throw new ArgumentNullException(nameof(presentationScheduler));
            _presentationClock = presentationClock ?? throw new ArgumentNullException(nameof(presentationClock));
            _projectionCommitSubscription = projectionCommitSubscription ?? throw new ArgumentNullException(nameof(projectionCommitSubscription));
            _framePump = framePump ?? throw new ArgumentNullException(nameof(framePump));
            _shellController = shellController ?? throw new ArgumentNullException(nameof(shellController));
            _shutdownController = shutdownController ?? throw new ArgumentNullException(nameof(shutdownController));
            _applicationSession = applicationSession ?? throw new ArgumentNullException(nameof(applicationSession));
            _hideOverlay = hideOverlay ?? throw new ArgumentNullException(nameof(hideOverlay));
        }

        public bool IsShutdownComplete => _coordinator.IsShutdownComplete;

        public void Start()
        {
            if (_started) return;
            _started = true;
            _presentationClock.Tick += OnPresentationClock;
            _coordinator.RegisterStateCommitSink(_presentationScheduler);
            if (_telemetry != null) _coordinator.RegisterStateCommitSink(_telemetry);
        }

        public void MarkCurrentStateCommitted() =>
            _presentationScheduler.MarkOverlayStateCommitted(_coordinator.Latest);

        public void ClearStableSnapshot(string message) =>
            _coordinator.ClearStableSnapshot(message);

        public void BeginShutdown()
        {
            if (_shutdownPrepared) return;
            _shellController.PrepareForRuntimeShutdown();
            _projectionCommitSubscription.Dispose();
            _framePump.Stop();
            _coordinator.UnregisterStateCommitSink(_presentationScheduler);
            if (_telemetry != null) _coordinator.UnregisterStateCommitSink(_telemetry);
            _presentationScheduler.Stop();
            _presentationClock.Stop();
            _presentationClock.Tick -= OnPresentationClock;
            _hideOverlay();
            _shutdownPrepared = true;
        }

        public OverlayShutdownResult TryStop() =>
            _shutdownController.TryStopApplication(_coordinator);

        public bool Complete()
        {
            if (_completed) return true;
            if (!_coordinator.IsShutdownComplete) return false;
            _applicationSession.Dispose();
            _completed = true;
            return true;
        }

        public void Dispose()
        {
            if (_completed) return;
            BeginShutdown();
            if (TryStop().IsComplete) Complete();
        }

        private void OnPresentationClock(object sender, EventArgs args) =>
            _presentationScheduler.MarkClock();
    }
}
