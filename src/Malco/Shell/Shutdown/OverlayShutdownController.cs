using Malco.Game.Services;

namespace Malco.Shell.Shutdown
{
    internal readonly struct OverlayShutdownResult
    {
        public OverlayShutdownResult(bool isComplete, string message)
        {
            IsComplete = isComplete;
            Message = message ?? string.Empty;
        }

        public bool IsComplete { get; }
        public string Message { get; }
    }

    internal sealed class OverlayShutdownController
    {
        public OverlayShutdownResult TryStopApplication(GameCoordinator coordinator)
        {
            if (coordinator == null)
            {
                return new OverlayShutdownResult(true, string.Empty);
            }

            coordinator.Dispose();
            if (!coordinator.IsShutdownComplete || coordinator.ShutdownBlocked)
            {
                var detail = coordinator.ShutdownFailureMessage;
                return new OverlayShutdownResult(
                    false,
                    string.IsNullOrWhiteSpace(detail)
                        ? "Application/provider shutdown is blocked. Choose Retry Quit from the tray."
                        : detail + " Choose Retry Quit from the tray.");
            }

            return new OverlayShutdownResult(true, string.Empty);
        }
    }
}
