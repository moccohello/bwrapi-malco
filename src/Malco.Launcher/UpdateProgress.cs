namespace Malco.Launcher
{
    internal enum UpdateStage
    {
        Preparing,
        Downloading,
        Verifying,
        Finalizing,
        Completed
    }

    internal readonly struct UpdateProgress
    {
        public UpdateProgress(UpdateStage stage, long completedBytes = 0, long totalBytes = 0)
        {
            Stage = stage;
            CompletedBytes = completedBytes;
            TotalBytes = totalBytes;
        }

        public UpdateStage Stage { get; }
        public long CompletedBytes { get; }
        public long TotalBytes { get; }

        public int Percentage
        {
            get
            {
                if (TotalBytes <= 0) return 0;
                var value = CompletedBytes * 100L / TotalBytes;
                if (value < 0) return 0;
                return value > 100 ? 100 : (int)value;
            }
        }
    }
}
