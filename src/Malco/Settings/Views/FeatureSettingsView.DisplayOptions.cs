using System;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Malco.Configuration.Models;
using Malco.Localization;
using Malco.Settings.Contracts;

namespace Malco.Settings.Views
{
    internal sealed partial class FeatureSettingsView
    {
        private FrameworkElement BuildWorkerCountStyleSelector()
        {
            var row = new Grid { Margin = new Thickness(0d, 20d, 0d, 0d) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var copy = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            copy.Children.Add(_palette.Text(UiText.Get("Worker count style"), 14d, FontWeights.SemiBold, _palette.TextBrush));
            copy.Children.Add(_palette.Text(
                UiText.Get("Choose how worker counts are displayed."),
                12d,
                FontWeights.Normal,
                _palette.MutedBrush));
            row.Children.Add(copy);

            var current = MalcoPreferenceValues.NormalizeWorkerCountStyle(_actions.Layout.WorkerCountStyle);
            var options = new StackPanel { Orientation = Orientation.Horizontal };
            AddWorkerCountStyleOption(
                options,
                current,
                MalcoPreferenceValues.WorkerCountClassicGreen,
                "Classic green");
            AddWorkerCountStyleOption(
                options,
                current,
                MalcoPreferenceValues.WorkerCountWhite,
                "White");
            if (_compactLayout)
            {
                var stack = new StackPanel();
                row.Children.Clear();
                stack.Children.Add(copy);
                options.Margin = new Thickness(0d, 12d, 0d, 0d);
                stack.Children.Add(options);
                row.Children.Add(stack);
            }
            else
            {
                Grid.SetColumn(options, 1);
                row.Children.Add(options);
            }

            return new Border
            {
                Padding = new Thickness(16d),
                Background = _palette.SurfaceBrush,
                BorderBrush = _palette.SeparatorBrush,
                BorderThickness = new Thickness(0d, 0d, 0d, 1d),
                Child = row
            };
        }

        private void AddWorkerCountStyleOption(
            Panel options,
            string current,
            string value,
            string labelKey)
        {
            var label = UiText.Get(labelKey);
            var button = SegmentButton(
                label,
                string.Equals(current, value, StringComparison.Ordinal),
                "settings-worker-count-style");
            button.Click += (sender, args) =>
            {
                if (!_actions.ApplyEdit(SettingsEdit.SetWorkerCountStyle(value)).Changed)
                {
                    return;
                }
                _actions.RefreshPresenterViews();
                RenderDetail();
                RestoreFocusAfterRender(label);
            };
            options.Children.Add(button);
        }

        private FrameworkElement BuildCompletionModeSelector()
        {
            var row = new Grid { Margin = new Thickness(0d, 20d, 0d, 0d) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var copy = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            copy.Children.Add(_palette.Text(UiText.Get("Completion display mode"), 14d, FontWeights.SemiBold, _palette.TextBrush));
            copy.Children.Add(_palette.Text(UiText.Get("Choose how active upgrades and research show progress."), 12d, FontWeights.Normal, _palette.MutedBrush));
            row.Children.Add(copy);
            var current = MalcoPreferenceValues.NormalizeCompletionMode(_actions.Layout.CompletionDisplayMode);
            var options = new StackPanel { Orientation = Orientation.Horizontal };
            var countdown = SegmentButton(UiText.Get("Countdown"),
                string.Equals(current, MalcoPreferenceValues.Countdown10Seconds, StringComparison.Ordinal),
                "settings-completion-mode");
            countdown.Click += (sender, args) =>
            {
                SetCompletionMode(MalcoPreferenceValues.Countdown10Seconds);
                RestoreFocusAfterRender(UiText.Get("Countdown"));
            };
            options.Children.Add(countdown);
            var progress = SegmentButton(UiText.Get("Full progress"),
                string.Equals(current, MalcoPreferenceValues.Progress, StringComparison.Ordinal),
                "settings-completion-mode");
            progress.Click += (sender, args) =>
            {
                SetCompletionMode(MalcoPreferenceValues.Progress);
                RestoreFocusAfterRender(UiText.Get("Full progress"));
            };
            options.Children.Add(progress);
            if (_compactLayout)
            {
                var stack = new StackPanel();
                row.Children.Clear();
                stack.Children.Add(copy);
                options.Margin = new Thickness(0d, 12d, 0d, 0d);
                stack.Children.Add(options);
                row.Children.Add(stack);
            }
            else
            {
                Grid.SetColumn(options, 1);
                row.Children.Add(options);
            }
            return new Border
            {
                Padding = new Thickness(16d),
                Background = _palette.SurfaceBrush,
                BorderBrush = _palette.SeparatorBrush,
                BorderThickness = new Thickness(0d, 0d, 0d, 1d),
                Child = row
            };
        }

        private FrameworkElement BuildIconSizeSelector(string featureKey)
        {
            var row = new Grid { Margin = new Thickness(0d, 20d, 0d, 0d) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var copy = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            copy.Children.Add(_palette.Text(UiText.Get("Icon size"), 14d, FontWeights.SemiBold, _palette.TextBrush));
            copy.Children.Add(_palette.Text(
                UiText.Get("Choose the icon size for this display."),
                12d,
                FontWeights.Normal,
                _palette.MutedBrush));
            row.Children.Add(copy);

            var current = _actions.Layout.GetIconSize(featureKey);
            var options = new StackPanel { Orientation = Orientation.Horizontal };
            AddIconSizeOption(options, featureKey, current, MalcoPreferenceValues.IconSmall, "Small");
            AddIconSizeOption(options, featureKey, current, MalcoPreferenceValues.IconMedium, "Medium");
            AddIconSizeOption(options, featureKey, current, MalcoPreferenceValues.IconLarge, "Large");
            if (_compactLayout)
            {
                var stack = new StackPanel();
                row.Children.Clear();
                stack.Children.Add(copy);
                options.Margin = new Thickness(0d, 12d, 0d, 0d);
                stack.Children.Add(options);
                row.Children.Add(stack);
            }
            else
            {
                Grid.SetColumn(options, 1);
                row.Children.Add(options);
            }

            return new Border
            {
                Padding = new Thickness(16d),
                Background = _palette.SurfaceBrush,
                BorderBrush = _palette.SeparatorBrush,
                BorderThickness = new Thickness(0d, 0d, 0d, 1d),
                Child = row
            };
        }

        private void AddIconSizeOption(
            Panel options,
            string featureKey,
            string current,
            string value,
            string labelKey)
        {
            var label = UiText.Get(labelKey);
            var button = SegmentButton(
                label,
                string.Equals(current, value, StringComparison.Ordinal),
                "settings-icon-size");
            button.Click += (sender, args) =>
            {
                if (!_actions.ApplyEdit(SettingsEdit.SetIconSize(featureKey, value)).Changed)
                {
                    return;
                }
                _actions.RefreshPresenterViews();
                RenderDetail();
                RestoreFocusAfterRender(label);
            };
            options.Children.Add(button);
        }

        private FrameworkElement BuildCompletionCountdownSelector()
        {
            var current = MalcoPreferenceValues.NormalizeCompletionCountdownSeconds(
                _actions.Layout.CompletionCountdownSeconds);
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var copy = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            copy.Children.Add(_palette.Text(UiText.Get("Countdown duration"), 14d, FontWeights.SemiBold, _palette.TextBrush));
            copy.Children.Add(_palette.Text(
                UiText.Get("Choose when the completion countdown appears (5-30 seconds)."),
                12d,
                FontWeights.Normal,
                _palette.MutedBrush));
            row.Children.Add(copy);

            var controls = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            var decreaseLabel = UiText.Get("Decrease countdown duration");
            var decrease = _chrome.ActionButton("-");
            decrease.Width = 44d;
            decrease.MinWidth = 44d;
            decrease.Height = 44d;
            decrease.IsEnabled = current > MalcoPreferenceValues.MinimumCompletionCountdownSeconds;
            AutomationProperties.SetName(decrease, decreaseLabel);
            decrease.Click += (sender, args) =>
            {
                SetCompletionCountdownSeconds(
                    current - MalcoPreferenceValues.CompletionCountdownStepSeconds);
                RestoreFocusAfterRender(decreaseLabel);
            };
            controls.Children.Add(decrease);

            var value = _palette.Text(
                string.Format(CultureInfo.CurrentCulture, UiText.Get("{0} seconds"), current),
                15d,
                FontWeights.Bold,
                _palette.TextBrush);
            value.Width = 82d;
            value.TextAlignment = TextAlignment.Center;
            value.VerticalAlignment = VerticalAlignment.Center;
            AutomationProperties.SetName(value, value.Text);
            controls.Children.Add(value);

            var increaseLabel = UiText.Get("Increase countdown duration");
            var increase = _chrome.ActionButton("+");
            increase.Width = 44d;
            increase.MinWidth = 44d;
            increase.Height = 44d;
            increase.IsEnabled = current < MalcoPreferenceValues.MaximumCompletionCountdownSeconds;
            AutomationProperties.SetName(increase, increaseLabel);
            increase.Click += (sender, args) =>
            {
                SetCompletionCountdownSeconds(
                    current + MalcoPreferenceValues.CompletionCountdownStepSeconds);
                RestoreFocusAfterRender(increaseLabel);
            };
            controls.Children.Add(increase);

            if (_compactLayout)
            {
                var stack = new StackPanel();
                row.Children.Clear();
                stack.Children.Add(copy);
                controls.Margin = new Thickness(0d, 12d, 0d, 0d);
                stack.Children.Add(controls);
                row.Children.Add(stack);
            }
            else
            {
                Grid.SetColumn(controls, 1);
                row.Children.Add(controls);
            }

            return new Border
            {
                Padding = new Thickness(16d),
                Background = _palette.SurfaceBrush,
                BorderBrush = _palette.SeparatorBrush,
                BorderThickness = new Thickness(0d, 0d, 0d, 1d),
                Child = row
            };
        }
    }
}
