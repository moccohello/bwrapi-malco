using System.Globalization;
using System.Linq;
using Malco.Models;

namespace Malco.Configuration.Models
{
    internal enum UpgradeCompletionDisplayMode
    {
        Countdown10Seconds,
        Progress
    }

    internal static class MalcoPreferenceValues
    {
        public const string AbilityHidden = "hidden";
        public const string AbilityEnergy = "energy";
        public const string English = "en_US";
        public const string Korean = "ko_KR";
        public const string Countdown10Seconds = "countdown_10s";
        public const string Progress = "progress";
        public const string IconSmall = "small";
        public const string IconMedium = "medium";
        public const string IconLarge = "large";
        public const string WorkerCountClassicGreen = "classic-green";
        public const string WorkerCountWhite = "white";
        public const int MinimumCompletionCountdownSeconds = 5;
        public const int MaximumCompletionCountdownSeconds = 30;
        public const int DefaultCompletionCountdownSeconds = 10;
        public const int CompletionCountdownStepSeconds = 5;

        public static string NormalizeLanguage(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                value = System.Environment.GetEnvironmentVariable("MALCO_UI_LANGUAGE");
            }

            if (string.Equals(value, Korean, System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "ko-KR", System.StringComparison.OrdinalIgnoreCase))
            {
                return Korean;
            }

            if (string.Equals(value, English, System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "en-US", System.StringComparison.OrdinalIgnoreCase))
            {
                return English;
            }

            return string.Equals(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName, "ko", System.StringComparison.OrdinalIgnoreCase)
                ? Korean
                : English;
        }

        public static string NormalizeCompletionMode(string value)
        {
            return string.Equals(value, Progress, System.StringComparison.OrdinalIgnoreCase)
                ? Progress
                : Countdown10Seconds;
        }

        public static string NormalizeAbilityDisplayMode(string value)
        {
            if (string.Equals(value, AbilityEnergy, System.StringComparison.OrdinalIgnoreCase))
                return AbilityEnergy;
            if (!string.IsNullOrWhiteSpace(value) && value.StartsWith("tech:", System.StringComparison.OrdinalIgnoreCase))
                return value.ToLowerInvariant();
            return AbilityHidden;
        }

        public static string NormalizeAbilityDisplayModeForUnit(int unitId, string value)
        {
            var normalized = NormalizeAbilityDisplayMode(value);
            var definition = AbilityCatalog.Find(unitId);
            if (definition == null)
                return AbilityHidden;
            if (!normalized.StartsWith("tech:", System.StringComparison.Ordinal))
                return normalized;

            return definition.Abilities.Any(ability =>
                       string.Equals(ability.Mode, normalized, System.StringComparison.Ordinal))
                ? normalized
                : AbilityHidden;
        }

        public static UpgradeCompletionDisplayMode ParseCompletionMode(string value)
        {
            return string.Equals(NormalizeCompletionMode(value), Progress, System.StringComparison.Ordinal)
                ? UpgradeCompletionDisplayMode.Progress
                : UpgradeCompletionDisplayMode.Countdown10Seconds;
        }

        public static int NormalizeCompletionCountdownSeconds(int value)
        {
            var clamped = System.Math.Max(
                MinimumCompletionCountdownSeconds,
                System.Math.Min(MaximumCompletionCountdownSeconds, value));
            return ((clamped + CompletionCountdownStepSeconds / 2) /
                    CompletionCountdownStepSeconds) *
                   CompletionCountdownStepSeconds;
        }

        public static string NormalizeIconSize(string value, string fallback)
        {
            if (string.Equals(value, IconSmall, System.StringComparison.OrdinalIgnoreCase))
                return IconSmall;
            if (string.Equals(value, IconMedium, System.StringComparison.OrdinalIgnoreCase))
                return IconMedium;
            if (string.Equals(value, IconLarge, System.StringComparison.OrdinalIgnoreCase))
                return IconLarge;
            return string.Equals(fallback, IconSmall, System.StringComparison.OrdinalIgnoreCase)
                ? IconSmall
                : string.Equals(fallback, IconMedium, System.StringComparison.OrdinalIgnoreCase)
                    ? IconMedium
                    : IconLarge;
        }

        public static string NormalizeWorkerCountStyle(string value)
        {
            return string.Equals(value, WorkerCountWhite, System.StringComparison.OrdinalIgnoreCase)
                ? WorkerCountWhite
                : WorkerCountClassicGreen;
        }

        public static double IconTileWidth(string value)
        {
            switch (NormalizeIconSize(value, IconLarge))
            {
                case IconSmall: return 22d;
                case IconMedium: return 30d;
                default: return 38d;
            }
        }

        public static double IconTileGap(string value)
        {
            switch (NormalizeIconSize(value, IconLarge))
            {
                case IconSmall: return 1d;
                case IconMedium: return 2d;
                default: return 6d;
            }
        }
    }
}
