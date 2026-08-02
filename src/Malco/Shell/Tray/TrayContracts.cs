namespace Malco.Shell.Tray
{
    internal interface ITrayIntentSink
    {
        void OpenSettings();

        void RequestQuit();
    }
}
