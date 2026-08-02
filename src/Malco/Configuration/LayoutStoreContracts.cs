namespace Malco.Configuration
{
    internal enum LayoutLoadStatus
    {
        Missing,
        Loaded,
        ResetToDefaults,
        CorruptSourcePreserved,
        NewerSchemaPreserved
    }

    internal sealed class LayoutLoadResult
    {
        public LayoutLoadResult(
            LayoutLoadStatus status,
            HudLayoutConfig layout,
            bool isWriteBlocked,
            string message)
        {
            Status = status;
            Layout = layout ?? HudLayoutConfig.CreateDefault();
            IsWriteBlocked = isWriteBlocked;
            Message = message ?? string.Empty;
        }

        public LayoutLoadStatus Status { get; }
        public HudLayoutConfig Layout { get; }
        public bool IsWriteBlocked { get; }
        public string Message { get; }
    }

    internal enum LayoutSaveStatus
    {
        Saved,
        Failed,
        WriteBlocked
    }

    internal readonly struct LayoutSaveResult
    {
        public LayoutSaveResult(LayoutSaveStatus status, string message)
        {
            Status = status;
            Message = message ?? string.Empty;
        }

        public LayoutSaveStatus Status { get; }
        public string Message { get; }
        public bool Succeeded { get { return Status == LayoutSaveStatus.Saved; } }
    }
}
