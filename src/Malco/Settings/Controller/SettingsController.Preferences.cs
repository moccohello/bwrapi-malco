using System;
using Malco.Configuration.Models;

namespace Malco.Settings.Controller
{
    internal sealed partial class SettingsController
    {
        private bool SetItemShown(string key, bool shown)
        {
            if (_layout.IsItemShown(key) == shown)
            {
                return false;
            }

            _layout.SetItemShown(key, shown);
            return true;
        }

        private bool SetItemsShown(string[] keys, bool shown)
        {
            var changed = false;
            foreach (var key in keys ?? Array.Empty<string>())
            {
                changed |= SetItemShown(key, shown);
            }
            return changed;
        }

        private bool SetAbilityDisplayMode(string key, string mode)
        {
            return _layout.SetAbilityDisplayMode(key, mode);
        }

        private bool SetIconSize(string key, string size)
        {
            return _layout.SetIconSize(key, size);
        }

        private bool SetWorkerCountStyle(string style)
        {
            var normalized = MalcoPreferenceValues.NormalizeWorkerCountStyle(style);
            if (string.Equals(_layout.WorkerCountStyle, normalized, StringComparison.Ordinal))
            {
                return false;
            }

            _layout.WorkerCountStyle = normalized;
            return true;
        }

        private bool SetTransportCargoVisible(bool visible)
        {
            if (_layout.ShowTransportCargo == visible)
            {
                return false;
            }

            _layout.ShowTransportCargo = visible;
            return true;
        }

        private bool SetAvailableAlert(string key, bool enabled)
        {
            if (_layout.IsAvailableUpgradeAlertEnabled(key) == enabled)
            {
                return false;
            }

            _layout.SetAvailableUpgradeAlert(key, enabled);
            return true;
        }

        private bool SetAvailableAlerts(string[] keys, bool enabled)
        {
            var changed = false;
            foreach (var key in keys ?? Array.Empty<string>())
            {
                changed |= SetAvailableAlert(key, enabled);
            }
            return changed;
        }

        private bool SetCompletionAlert(string key, bool enabled)
        {
            if (_layout.IsCompletionWarningEnabled(key) == enabled)
            {
                return false;
            }

            _layout.SetCompletionWarning(key, enabled);
            return true;
        }

        private bool SetCompletionAlerts(string[] keys, bool enabled)
        {
            var changed = false;
            foreach (var key in keys ?? Array.Empty<string>())
            {
                changed |= SetCompletionAlert(key, enabled);
            }
            return changed;
        }

        private bool SetLanguage(string language)
        {
            var normalized = MalcoPreferenceValues.NormalizeLanguage(language);
            if (string.Equals(_layout.Language, normalized, StringComparison.Ordinal))
            {
                return false;
            }

            _layout.Language = normalized;
            return true;
        }

        private bool SetCompletionDisplayMode(string mode)
        {
            var normalized = MalcoPreferenceValues.NormalizeCompletionMode(mode);
            if (string.Equals(_layout.CompletionDisplayMode, normalized, StringComparison.Ordinal))
            {
                return false;
            }

            _layout.CompletionDisplayMode = normalized;
            return true;
        }

        private bool SetCompletionCountdownSeconds(int seconds)
        {
            var normalized = MalcoPreferenceValues.NormalizeCompletionCountdownSeconds(seconds);
            if (_layout.CompletionCountdownSeconds == normalized)
            {
                return false;
            }

            _layout.CompletionCountdownSeconds = normalized;
            return true;
        }
    }
}
