namespace Malco.Shell.Control
{
    internal static class MalcoControlProtocol
    {
        private static readonly int SessionId =
            MalcoControlPeerVerifier.GetCurrentSessionId();
        private static readonly string SessionPipeName =
            "Malco.Control.v1.Session-" + SessionId;

        internal static string PipeName => SessionPipeName;
        internal static int InteractiveSessionId => SessionId;

        internal static MalcoControlExitCode RequestQuit(string mutexName)
        {
            return MalcoControlClient.RequestQuit(
                mutexName,
                PipeName,
                InteractiveSessionId);
        }

    }
}
