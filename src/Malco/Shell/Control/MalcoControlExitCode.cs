namespace Malco.Shell.Control
{
    internal enum MalcoControlExitCode
    {
        Success = 0,
        NotRunning = 2,
        Timeout = 3,
        ProtocolError = 4,
        Refused = 5,
        Usage = 64
    }
}
