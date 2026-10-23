using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Malco.Localization;
using Malco.Models;
using Malco.Settings.Contracts;

namespace Malco.Settings.Views
{
    internal sealed partial class FeatureSettingsView
    {
        private FrameworkElement BuildZergProductionSettings()
        {
            var settings = new StackPanel { Margin = new Thickness(0d, 20d, 0d, 0d) };
            var layout = _actions.LayoutSnapshot;
            settings.Children.Add(BuildSwitchRow(
                _previewFactory.BuildFeatureIcon(FeatureSettingsPreviewKind.ZergProduction, 40d),
                UiText.Get("Larva countdown"),
                UiText.Get("Show the in-game time remaining until the next larva spawns below Hatcheries, Lairs, and Hives."),
                layout.ShowLarvaCountdown,
                next => SettingsEdit.SetLarvaCountdownVisible(next),
                true));
            settings.Children.Add(BuildSwitchRow(
                _previewFactory.BuildCatalogIcon(TechTreeItem.Unit(36), 40d),
                UiText.Get("Morph target icons"),
                UiText.Get("Show the target unit icon above eggs and cocoons."),
                layout.ShowMorphTargetIcons,
                next => SettingsEdit.SetMorphTargetIconsVisible(next),
                false));
            return settings;
        }

        // description이 null이면 항목 행(작은 아이콘·한 줄)으로 그린다.
        private FrameworkElement BuildSwitchRow(
            FrameworkElement icon,
            string title,
            string description,
            bool enabled,
            Func<bool, SettingsEdit> createEdit,
            bool first)
        {
            var compact = description == null;
            var row = new Grid { Margin = compact ? new Thickness(16d, 8d, 16d, 8d) : new Thickness(16d) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(compact ? 44d : 52d) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            icon.VerticalAlignment = VerticalAlignment.Center;
            icon.HorizontalAlignment = HorizontalAlignment.Left;
            row.Children.Add(icon);

            var copy = new StackPanel
            {
                Margin = new Thickness(12d, 0d, 16d, 0d),
                VerticalAlignment = VerticalAlignment.Center
            };
            copy.Children.Add(_palette.Text(
                title,
                compact ? 13d : 14d,
                compact ? FontWeights.SemiBold : FontWeights.Bold,
                _palette.TextBrush));
            if (!compact)
            {
                var descriptionText = _palette.Text(description, 12d, FontWeights.Normal, _palette.MutedBrush);
                descriptionText.Margin = new Thickness(0d, 3d, 0d, 0d);
                descriptionText.TextWrapping = TextWrapping.Wrap;
                copy.Children.Add(descriptionText);
            }
            Grid.SetColumn(copy, 1);
            row.Children.Add(copy);

            var switchName = UiText.Get("Show in game") + ": " + title;
            var switchHost = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            var state = _palette.Text(UiText.Get(enabled ? "On" : "Off"), 12d, FontWeights.SemiBold,
                _palette.MutedBrush);
            state.VerticalAlignment = VerticalAlignment.Center;
            state.Margin = new Thickness(0d, 0d, 8d, 0d);
            switchHost.Children.Add(state);
            switchHost.Children.Add(BuildSwitch(
                switchName,
                enabled,
                next =>
                {
                    _actions.ApplyEdit(createEdit(next));
                    _actions.RefreshPresenterViews();
                    _actions.RefreshSpatialPresentation();
                    _actions.RefreshVisibility();
                    Refresh();
                    RestoreFocusAfterRender(switchName);
                }));
            Grid.SetColumn(switchHost, 2);
            row.Children.Add(switchHost);

            var setting = new Border
            {
                Background = _palette.SurfaceBrush,
                BorderBrush = _palette.SeparatorBrush,
                BorderThickness = first ? new Thickness(0d, 1d, 0d, 1d) : new Thickness(0d, 0d, 0d, 1d),
                Child = row
            };
            AutomationProperties.SetName(setting, title);
            return setting;
        }

        private FrameworkElement BuildSectionTitle(string text)
        {
            var title = _palette.Text(text, 13d, FontWeights.Bold, _palette.MutedBrush);
            title.Margin = new Thickness(4d, 20d, 0d, 8d);
            return title;
        }
    }
}
