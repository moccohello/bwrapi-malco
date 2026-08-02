namespace Malco
{
    internal sealed class HudItemSetting
    {
        public HudItemSetting()
        {
            Show = true;
            AvailableAlert = true;
            CompletionAlert = true;
        }

        public bool Show { get; set; }

        public bool AvailableAlert { get; set; }

        public bool CompletionAlert { get; set; }
    }
}
