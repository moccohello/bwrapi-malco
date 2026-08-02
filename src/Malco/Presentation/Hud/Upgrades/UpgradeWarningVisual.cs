using System;
using System.Windows.Controls;
using System.Windows;
using Malco.Models;

namespace Malco.Presentation.Hud.Upgrades
{
    internal sealed class UpgradeWarningVisual
    {
        public UpgradeWarningVisual(
            string key,
            UpgradeState state,
            DateTime capturedAt,
            Border root,
            TextBlock value,
            ProgressBar progress)
        {
            Key = key;
            State = state;
            CapturedAt = capturedAt;
            Root = root;
            Value = value;
            Progress = progress;
        }

        public string Key { get; private set; }

        public UpgradeState State { get; private set; }

        public DateTime CapturedAt { get; private set; }

        public Border Root { get; private set; }

        public TextBlock Value { get; private set; }

        public ProgressBar Progress { get; private set; }

        public void SetCountdownValue(string text)
        {
            Value.Text = text ?? string.Empty;
            Progress.Visibility = Visibility.Collapsed;
        }

        public void SetProgressValue(double percent, string remaining)
        {
            var clamped = Math.Min(100d, Math.Max(0d, percent));
            Value.Text = clamped.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + "%  " +
                         (remaining ?? string.Empty);
            Progress.Value = clamped;
            Progress.Visibility = Visibility.Visible;
        }

        public void Update(UpgradeState state, DateTime capturedAt)
        {
            State = state;
            CapturedAt = capturedAt;
            if (Root != null)
            {
                Root.ToolTip = state != null
                    ? UpgradeTileFactory.LocalizedName(state)
                    : null;
            }
        }
    }
}
