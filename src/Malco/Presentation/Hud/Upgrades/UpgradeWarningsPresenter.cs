using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Malco.Models;
using Malco.Configuration.Models;

namespace Malco.Presentation.Hud.Upgrades
{
    internal sealed class UpgradeWarningsPresenter
    {
        private readonly UpgradeTileFactory _tiles;
        private readonly List<UpgradeWarningVisual> _ordered = new List<UpgradeWarningVisual>();
        private readonly Dictionary<string, UpgradeWarningVisual> _byKey = new Dictionary<string, UpgradeWarningVisual>(StringComparer.OrdinalIgnoreCase);
        private UpgradeCompletionDisplayMode _displayMode = UpgradeCompletionDisplayMode.Countdown10Seconds;
        private int _countdownWindowSeconds = MalcoPreferenceValues.DefaultCompletionCountdownSeconds;

        public UpgradeWarningsPresenter(UpgradeTileFactory tiles)
        {
            _tiles = tiles ?? throw new ArgumentNullException(nameof(tiles));
            View = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                IsHitTestVisible = false,
                Visibility = Visibility.Collapsed
            };
        }

        public StackPanel View { get; }

        public bool NeedsClockRefresh => _ordered.Count != 0;

        public bool Apply(UpgradePresentationInput input, IList<UpgradeState> states)
        {
            if (!input.Preferences.IsWidgetEnabled(HudWidgetRegistry.UpgradeCompletionWarnings))
            {
                Clear();
                return false;
            }
            _displayMode = input.Preferences.CompletionDisplayMode;
            _countdownWindowSeconds = input.Preferences.CompletionCountdownSeconds;
            var dueSoon = (states ?? new List<UpgradeState>())
                .Where(state => state != null &&
                    !state.IsComplete &&
                    input.Preferences.IsCompletionWarningEnabled(UpgradePresentationIdentity.ForState(state)) &&
                    IsVisibleForMode(state))
                .OrderBy(state => _displayMode == UpgradeCompletionDisplayMode.Progress
                    ? -state.ProgressPercent
                    : UpgradePresentationIdentity.RawRemainingSeconds(state))
                .ToList();
            if (dueSoon.Count == 0)
            {
                Clear();
                return false;
            }
            var previousTops = CaptureTops();
            var next = new List<UpgradeWarningVisual>();
            var nextKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var capturedAt = input.Snapshot != null ? input.Snapshot.CapturedAt : DateTime.Now;
            foreach (var state in dueSoon)
            {
                var key = UpgradePresentationIdentity.WarningKey(state);
                nextKeys.Add(key);
                UpgradeWarningVisual warning;
                if (_byKey.TryGetValue(key, out warning)) warning.Update(state, capturedAt);
                else
                {
                    warning = _tiles.BuildWarning(key, state, capturedAt);
                    warning.Root.Opacity = 0d;
                    _byKey[key] = warning;
                    AnimateOpacity(warning.Root, 1d);
                }
                ApplyDisplayValue(warning, capturedAt);
                MoveChildToIndex(warning.Root, next.Count);
                next.Add(warning);
            }
            foreach (var stale in _byKey.Keys.Where(key => !nextKeys.Contains(key)).ToList())
            {
                View.Children.Remove(_byKey[stale].Root);
                _byKey.Remove(stale);
            }
            _ordered.Clear();
            _ordered.AddRange(next);
            AnimateReorder(previousTops);
            View.Visibility = Visibility.Visible;
            AdvanceClock(DateTime.Now);
            return true;
        }

        public void AdvanceClock(DateTime now)
        {
            if (_displayMode == UpgradeCompletionDisplayMode.Progress)
            {
                foreach (var warning in _ordered)
                {
                    warning.SetProgressValue(
                        warning.State != null ? warning.State.ProgressPercent : 0d,
                        UpgradePresentationIdentity.FormatRemainingSeconds(RemainingSeconds(warning, now)));
                }
                return;
            }

            for (var index = _ordered.Count - 1; index >= 0; index--)
            {
                var warning = _ordered[index];
                var elapsed = Math.Max(0d, (now - warning.CapturedAt).TotalSeconds);
                var remaining = Math.Max(0d, UpgradePresentationIdentity.RawRemainingSeconds(warning.State) - elapsed);
                if (remaining <= 0d)
                {
                    View.Children.Remove(warning.Root);
                    _byKey.Remove(warning.Key);
                    _ordered.RemoveAt(index);
                    continue;
                }
                var text = UpgradePresentationIdentity.FormatRemainingSeconds(remaining);
                if (!string.Equals(warning.Value.Text, text, StringComparison.Ordinal))
                {
                    warning.SetCountdownValue(text);
                }
            }
            if (_ordered.Count == 0) View.Visibility = Visibility.Collapsed;
        }

        public void Clear()
        {
            View.Children.Clear();
            _ordered.Clear();
            _byKey.Clear();
            View.Visibility = Visibility.Collapsed;
        }

        private bool IsVisibleForMode(UpgradeState state)
        {
            return _displayMode == UpgradeCompletionDisplayMode.Progress
                ? state.IsInProgress && state.ProgressPercent >= 0d && state.ProgressPercent <= 100d
                : UpgradePresentationIdentity.IsInCompletionWarningWindow(state, _countdownWindowSeconds);
        }

        private void ApplyDisplayValue(UpgradeWarningVisual warning, DateTime now)
        {
            if (_displayMode == UpgradeCompletionDisplayMode.Progress)
            {
                warning.SetProgressValue(
                    warning.State != null ? warning.State.ProgressPercent : 0d,
                    UpgradePresentationIdentity.FormatRemainingSeconds(RemainingSeconds(warning, now)));
                return;
            }

            warning.SetCountdownValue(UpgradePresentationIdentity.FormatRemainingSeconds(RemainingSeconds(warning, now)));
        }

        private static double RemainingSeconds(UpgradeWarningVisual warning, DateTime now)
        {
            var elapsed = Math.Max(0d, (now - warning.CapturedAt).TotalSeconds);
            return Math.Max(0d, UpgradePresentationIdentity.RawRemainingSeconds(warning.State) - elapsed);
        }

        private Dictionary<string, double> CaptureTops()
        {
            var tops = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (var warning in _byKey.Values)
            {
                if (warning == null || warning.Root == null || !View.Children.Contains(warning.Root)) continue;
                try { tops[warning.Key] = warning.Root.TranslatePoint(new Point(0d, 0d), View).Y; }
                catch (InvalidOperationException) { }
            }
            return tops;
        }

        private void MoveChildToIndex(UIElement child, int index)
        {
            var currentIndex = View.Children.IndexOf(child);
            if (currentIndex == index) return;
            if (currentIndex >= 0)
            {
                View.Children.RemoveAt(currentIndex);
                if (currentIndex < index) index--;
            }
            View.Children.Insert(Math.Min(index, View.Children.Count), child);
        }

        private void AnimateReorder(IDictionary<string, double> previousTops)
        {
            if (previousTops == null || previousTops.Count == 0) return;
            View.UpdateLayout();
            foreach (var warning in _ordered)
            {
                double previousTop;
                if (warning == null || warning.Root == null || !previousTops.TryGetValue(warning.Key, out previousTop)) continue;
                var currentTop = warning.Root.TranslatePoint(new Point(0d, 0d), View).Y;
                var delta = previousTop - currentTop;
                if (Math.Abs(delta) < 1d) continue;
                var transform = warning.Root.RenderTransform as TranslateTransform;
                if (transform == null)
                {
                    transform = new TranslateTransform();
                    warning.Root.RenderTransform = transform;
                }
                transform.Y = delta;
                transform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation
                {
                    To = 0d,
                    Duration = TimeSpan.FromMilliseconds(180),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                });
            }
        }

        private static void AnimateOpacity(UIElement element, double to)
        {
            if (element == null) return;
            element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation
            {
                To = to,
                Duration = TimeSpan.FromMilliseconds(140),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
        }
    }
}
