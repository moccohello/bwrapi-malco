namespace Malco.Settings.Contracts
{
    internal enum SettingsEditKind
    {
        SetWidgetEnabled,
        SetWidgetBounds,
        SetItemShown,
        SetItemsShown,
        SetAvailableAlert,
        SetAvailableAlerts,
        SetCompletionAlert,
        SetCompletionAlerts,
        SetLanguage,
        SetCompletionDisplayMode,
        SetCompletionCountdownSeconds,
        SetIconSize,
        SetWorkerCountStyle,
        SetAbilityDisplayMode,
        SetTransportCargoVisible,
        ResetWidgetBounds,
        ResetAllWidgetBounds
    }

    internal readonly struct WidgetBoundsValue
    {
        public WidgetBoundsValue(double x, double y, double width, double height)
            : this(x, y, width, height, false, 0d, 0d, 0d, 0d)
        {
        }

        public WidgetBoundsValue(
            double x,
            double y,
            double width,
            double height,
            bool hasRelativeBounds,
            double xRatio,
            double yRatio,
            double widthRatio,
            double heightRatio)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
            HasRelativeBounds = hasRelativeBounds;
            XRatio = xRatio;
            YRatio = yRatio;
            WidthRatio = widthRatio;
            HeightRatio = heightRatio;
        }

        public double X { get; }

        public double Y { get; }

        public double Width { get; }

        public double Height { get; }

        public bool HasRelativeBounds { get; }

        public double XRatio { get; }

        public double YRatio { get; }

        public double WidthRatio { get; }

        public double HeightRatio { get; }
    }

    internal sealed class SettingsEdit
    {
        private readonly string[] _keys;

        private SettingsEdit(
            SettingsEditKind kind,
            string key,
            bool booleanValue,
            string stringValue,
            int integerValue,
            WidgetBoundsValue bounds,
            string[] keys = null)
        {
            Kind = kind;
            Key = key ?? string.Empty;
            BooleanValue = booleanValue;
            StringValue = stringValue ?? string.Empty;
            IntegerValue = integerValue;
            Bounds = bounds;
            _keys = keys == null ? System.Array.Empty<string>() : (string[])keys.Clone();
        }

        public SettingsEditKind Kind { get; }

        public string Key { get; }

        public bool BooleanValue { get; }

        public string StringValue { get; }

        public int IntegerValue { get; }

        public WidgetBoundsValue Bounds { get; }

        public string[] Keys => (string[])_keys.Clone();

        public bool HasTarget
        {
            get
            {
                switch (Kind)
                {
                    case SettingsEditKind.SetItemsShown:
                    case SettingsEditKind.SetAvailableAlerts:
                    case SettingsEditKind.SetCompletionAlerts:
                        if (_keys.Length == 0)
                        {
                            return false;
                        }
                        foreach (var key in _keys)
                        {
                            if (string.IsNullOrWhiteSpace(key))
                            {
                                return false;
                            }
                        }
                        return true;
                    default:
                        return !string.IsNullOrWhiteSpace(Key);
                }
            }
        }

        public static SettingsEdit SetWidgetEnabled(string key, bool enabled)
        {
            return new SettingsEdit(SettingsEditKind.SetWidgetEnabled, key, enabled, string.Empty, 0, default);
        }

        public static SettingsEdit SetWidgetBounds(string key, WidgetBoundsValue bounds)
        {
            return new SettingsEdit(SettingsEditKind.SetWidgetBounds, key, false, string.Empty, 0, bounds);
        }

        public static SettingsEdit SetItemShown(string key, bool shown)
        {
            return new SettingsEdit(SettingsEditKind.SetItemShown, key, shown, string.Empty, 0, default);
        }

        public static SettingsEdit SetItemsShown(string[] keys, bool shown)
        {
            return new SettingsEdit(SettingsEditKind.SetItemsShown, string.Empty, shown, string.Empty, 0, default, keys);
        }

        public static SettingsEdit SetAvailableAlert(string key, bool enabled)
        {
            return new SettingsEdit(SettingsEditKind.SetAvailableAlert, key, enabled, string.Empty, 0, default);
        }

        public static SettingsEdit SetAvailableAlerts(string[] keys, bool enabled)
        {
            return new SettingsEdit(SettingsEditKind.SetAvailableAlerts, string.Empty, enabled, string.Empty, 0, default, keys);
        }

        public static SettingsEdit SetCompletionAlert(string key, bool enabled)
        {
            return new SettingsEdit(SettingsEditKind.SetCompletionAlert, key, enabled, string.Empty, 0, default);
        }

        public static SettingsEdit SetCompletionAlerts(string[] keys, bool enabled)
        {
            return new SettingsEdit(SettingsEditKind.SetCompletionAlerts, string.Empty, enabled, string.Empty, 0, default, keys);
        }

        public static SettingsEdit SetLanguage(string language)
        {
            return new SettingsEdit(SettingsEditKind.SetLanguage, "language", false, language, 0, default);
        }

        public static SettingsEdit SetCompletionDisplayMode(string mode)
        {
            return new SettingsEdit(SettingsEditKind.SetCompletionDisplayMode, "completion-display-mode", false, mode, 0, default);
        }

        public static SettingsEdit SetCompletionCountdownSeconds(int seconds)
        {
            return new SettingsEdit(
                SettingsEditKind.SetCompletionCountdownSeconds,
                "completion-countdown-seconds",
                false,
                string.Empty,
                seconds,
                default);
        }

        public static SettingsEdit SetAbilityDisplayMode(string key, string mode)
        {
            return new SettingsEdit(SettingsEditKind.SetAbilityDisplayMode, key, false, mode, 0, default);
        }

        public static SettingsEdit SetIconSize(string key, string size)
        {
            return new SettingsEdit(SettingsEditKind.SetIconSize, key, false, size, 0, default);
        }

        public static SettingsEdit SetWorkerCountStyle(string style)
        {
            return new SettingsEdit(
                SettingsEditKind.SetWorkerCountStyle,
                "worker-count-style",
                false,
                style,
                0,
                default);
        }

        public static SettingsEdit SetTransportCargoVisible(bool visible)
        {
            return new SettingsEdit(SettingsEditKind.SetTransportCargoVisible, "transport-cargo", visible, string.Empty, 0, default);
        }

        public static SettingsEdit ResetWidgetBounds(string key)
        {
            return new SettingsEdit(SettingsEditKind.ResetWidgetBounds, key, false, string.Empty, 0, default);
        }

        public static SettingsEdit ResetAllWidgetBounds()
        {
            return new SettingsEdit(SettingsEditKind.ResetAllWidgetBounds, "all-widgets", false, string.Empty, 0, default);
        }
    }
}
