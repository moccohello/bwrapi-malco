using Malco.Game.Services;

namespace Malco.Shell.Shutdown
{
    internal enum OverlayShutdownStatus
    {
        Complete,
        Blocked
    }

    internal readonly struct OverlayShutdownResult
    {
        public OverlayShutdownResult(OverlayShutdownStatus status, string message)
        {
            Status = status;
            Message = message ?? string.Empty;
        }

        public OverlayShutdownStatus Status { get; }
        public string Message { get; }
        public bool IsComplete => Status == OverlayShutdownStatus.Complete;
    }

    internal sealed class OverlayShutdownController
    {
        public OverlayShutdownResult TryStopApplication(GameCoordinator coordinator)
        {
            if (coordinator == null)
            {
                return new OverlayShutdownResult(OverlayShutdownStatus.Complete, string.Empty);
            }

            coordinator.Dispose();
            if (!coordinator.IsShutdownComplete || coordinator.ShutdownBlocked)
            {
                var detail = coordinator.ShutdownFailureMessage;
                return new OverlayShutdownResult(
                    OverlayShutdownStatus.Blocked,
                    string.IsNullOrWhiteSpace(detail)
                        ? "Application/provider shutdown is blocked. Choose Retry Quit from the tray."
                        : detail + " Choose Retry Quit from the tray.");
            }

            return new OverlayShutdownResult(OverlayShutdownStatus.Complete, string.Empty);
        }
    }
}
