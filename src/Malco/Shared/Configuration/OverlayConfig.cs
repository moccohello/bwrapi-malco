namespace Malco.Configuration
{
    internal sealed class OverlayConfig
    {
        public const string RuntimeTargetProcessName = "StarCraft";
        public const string RuntimeTargetWindowTitle = "";
        public const int RuntimeSemanticSnapshotIntervalMs = 42;
        public const int RuntimeProviderShutdownTimeoutMs = 2000;

        public string TargetProcessName { get; } = RuntimeTargetProcessName;

        public string TargetWindowTitle { get; } = RuntimeTargetWindowTitle;

        public int SemanticSnapshotIntervalMs { get; } = RuntimeSemanticSnapshotIntervalMs;

        public int ProviderShutdownTimeoutMs { get; } = RuntimeProviderShutdownTimeoutMs;
    }
}
