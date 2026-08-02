namespace Malco.Settings.Contracts
{
    internal enum SettingsPage
    {
        Features,
        Layout
    }

    internal enum SettingsIntentKind
    {
        OpenFeatures,
        OpenLayout,
        OpenTechTree,
        ToggleEditor,
        ReturnToGame,
        CloseEditor
    }

    internal readonly struct SettingsIntent
    {
        public SettingsIntent(SettingsIntentKind kind)
        {
            Kind = kind;
        }

        public SettingsIntentKind Kind { get; }
    }
}
