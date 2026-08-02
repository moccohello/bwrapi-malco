namespace Malco.Configuration
{
    internal sealed class OverlayConfig
    {
        public const string RuntimeTargetProcessName = "StarCraft";
        public const string RuntimeTargetWindowTitle = "";
        public const int RuntimePollIntervalMs = 16;
        public const int RuntimeSemanticSnapshotIntervalMs = 42;
        public const int RuntimeProviderShutdownTimeoutMs = 2000;

        public OverlayConfig()
        {
            ApplyRuntimeDefaults();
        }

        public string TargetProcessName { get; set; }

        public string TargetWindowTitle { get; set; }

        public int PollIntervalMs { get; set; }

        public int SemanticSnapshotIntervalMs { get; set; }

        public int ProviderShutdownTimeoutMs { get; set; }

        public void Normalize()
        {
            ApplyRuntimeDefaults();
        }

        private void ApplyRuntimeDefaults()
        {
            TargetProcessName = RuntimeTargetProcessName;
            TargetWindowTitle = RuntimeTargetWindowTitle;
            PollIntervalMs = RuntimePollIntervalMs;
            SemanticSnapshotIntervalMs = RuntimeSemanticSnapshotIntervalMs;
            ProviderShutdownTimeoutMs = RuntimeProviderShutdownTimeoutMs;
        }
    }
}
