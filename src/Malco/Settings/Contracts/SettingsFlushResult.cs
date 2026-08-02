namespace Malco.Settings.Contracts
{
    internal enum SettingsFlushStatus
    {
        NoChanges,
        Saved,
        RecoveryRequired,
        Failed
    }

    internal enum SettingsFlushReason
    {
        Autosave,
        EditorExit,
        Shutdown
    }

    internal readonly struct SettingsFlushResult
    {
        public SettingsFlushResult(SettingsFlushStatus status, long flushedRevision, string message)
            : this(status, SettingsFlushReason.Autosave, flushedRevision, flushedRevision, message)
        {
        }

        public SettingsFlushResult(
            SettingsFlushStatus status,
            SettingsFlushReason reason,
            long flushedRevision,
            string message)
            : this(status, reason, flushedRevision, flushedRevision, message)
        {
        }

        public SettingsFlushResult(
            SettingsFlushStatus status,
            SettingsFlushReason reason,
            long flushedRevision,
            long attemptedRevision,
            string message)
        {
            Status = status;
            Reason = reason;
            FlushedRevision = flushedRevision;
            AttemptedRevision = attemptedRevision;
            Message = message ?? string.Empty;
        }

        public SettingsFlushStatus Status { get; }

        public SettingsFlushReason Reason { get; }

        public long FlushedRevision { get; }

        public long AttemptedRevision { get; }

        public string Message { get; }

        public bool Succeeded
        {
            get
            {
                return Status == SettingsFlushStatus.NoChanges ||
                       Status == SettingsFlushStatus.Saved;
            }
        }

        public bool ShouldVetoEditorExit
        {
            get { return Reason == SettingsFlushReason.EditorExit && !Succeeded; }
        }

        public bool ShouldContinueShutdown
        {
            get { return Reason == SettingsFlushReason.Shutdown && Succeeded; }
        }
    }
}
