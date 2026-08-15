using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Malco.Localization;

namespace Malco.Settings.Views
{
    internal sealed partial class FeatureSettingsView
    {
        private void RenderNavigation()
        {
            _navigationHost.Children.Clear();
            FocusButton = null;
            AddNavigationGroup(
                UiText.Get("Status"),
                HudFeatureCatalog.InGroup(FeatureSettingsGroup.Status));
            AddNavigationGroup(
                UiText.Get("Alerts"),
                HudFeatureCatalog.InGroup(FeatureSettingsGroup.Alerts));
            AddNavigationGroup(
                UiText.Get("Guides"),
                HudFeatureCatalog.InGroup(FeatureSettingsGroup.Guides));
            AddNavigationGroup(UiText.Get("General"), new[]
            {
                HudFeatureCatalog.General
            });
        }

        private void AddNavigationGroup(
            string title,
            IEnumerable<FeatureSettingsDefinition> features)
        {
            var heading = _palette.Text(title.ToUpperInvariant(), 11d, FontWeights.Bold, _palette.MutedBrush);
            heading.Margin = new Thickness(8d, 4d, 8d, 6d);
            _navigationHost.Children.Add(heading);
            foreach (var feature in features)
            {
                _navigationHost.Children.Add(NavigationButton(feature));
            }
        }

        private Button NavigationButton(FeatureSettingsDefinition feature)
        {
            var selected = string.Equals(feature.Key, _selectedFeatureKey, StringComparison.OrdinalIgnoreCase);
            var grid = new Grid
            {
                Margin = new Thickness(_ultraCompactLayout ? 4d : 10d, 0d, _ultraCompactLayout ? 4d : 10d, 0d)
            };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(_ultraCompactLayout ? 32d : 38d) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var icon = _previewFactory.BuildFeatureIcon(
                feature.PreviewKind,
                _ultraCompactLayout ? 24d : 28d,
                true);
            icon.VerticalAlignment = VerticalAlignment.Center;
            grid.Children.Add(icon);
            var label = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var title = _palette.Text(feature.Title, 13d, FontWeights.SemiBold, _palette.TextBrush);
            if (_compactLayout)
            {
                title.TextWrapping = TextWrapping.Wrap;
                title.TextTrimming = TextTrimming.None;
                title.MaxHeight = 34d;
            }
            label.Children.Add(title);
            var stateText = FeatureStateText(feature.Key);
            label.Children.Add(_palette.Text(stateText, 11d, FontWeights.Medium,
                selected ? _palette.AccentBrush : _palette.MutedBrush));
            Grid.SetColumn(label, 1);
            grid.Children.Add(label);
            var chevron = _palette.Text("›", 20d, FontWeights.SemiBold, _palette.MutedBrush);
            chevron.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(chevron, 2);
            grid.Children.Add(chevron);

            var button = new Button
            {
                Height = _compactLayout ? 68d : 58d,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0d, 0d, 0d, 4d),
                Padding = new Thickness(0d),
                Foreground = _palette.TextBrush,
                Background = selected ? _palette.SelectedSurfaceBrush : Brushes.Transparent,
                BorderBrush = selected ? _palette.AccentBrush : Brushes.Transparent,
                BorderThickness = selected ? new Thickness(3d, 0d, 0d, 0d) : new Thickness(0d),
                Content = grid,
                Style = _chrome.ButtonStyle(),
                Tag = feature.Key,
                ToolTip = feature.Title + Environment.NewLine + feature.Description
            };
            button.Click += (sender, args) =>
            {
                _selectedFeatureKey = feature.Key;
                _searchText = string.Empty;
                RefreshNavigationSelection();
                RenderDetail();
                var focusTarget = (FrameworkElement)_searchInput ?? button;
                RestoreFocusAfterRender(AutomationProperties.GetName(focusTarget));
            };
            AutomationProperties.SetName(button, feature.Title + ", " + stateText);
            AutomationProperties.SetItemStatus(button, selected ? UiText.Get("Selected") : UiText.Get("Not selected"));
            if (selected)
            {
                FocusButton = button;
            }
            return button;
        }

        private void RefreshNavigationSelection()
        {
            FocusButton = null;
            foreach (var button in _navigationHost.Children.OfType<Button>())
            {
                var selected = string.Equals(button.Tag as string, _selectedFeatureKey,
                    StringComparison.OrdinalIgnoreCase);
                button.Background = selected ? _palette.SelectedSurfaceBrush : Brushes.Transparent;
                button.BorderBrush = selected ? _palette.AccentBrush : Brushes.Transparent;
                button.BorderThickness = selected
                    ? new Thickness(3d, 0d, 0d, 0d)
                    : new Thickness(0d);
                AutomationProperties.SetItemStatus(button,
                    selected ? UiText.Get("Selected") : UiText.Get("Not selected"));

                var grid = button.Content as Grid;
                var label = grid?.Children.OfType<StackPanel>().FirstOrDefault();
                var state = label?.Children.OfType<TextBlock>().Skip(1).FirstOrDefault();
                if (state != null)
                {
                    state.Foreground = selected ? _palette.AccentBrush : _palette.MutedBrush;
                }

                var chevron = grid?.Children.OfType<TextBlock>().FirstOrDefault();
                if (chevron != null)
                {
                    chevron.Foreground = _palette.MutedBrush;
                }

                if (selected)
                {
                    FocusButton = button;
                }
            }
        }

        private string FeatureStateText(string featureKey)
        {
            var feature = HudFeatureCatalog.Find(featureKey);
            switch (feature?.DetailKind)
            {
                case FeatureSettingsDetailKind.General:
                    return UiText.Get("Preferences");
                case FeatureSettingsDetailKind.ResourceWorkers:
                    var minerals = _actions.IsFeatureEnabled(HudWidgetRegistry.MineralWorkers);
                    var gas = _actions.IsFeatureEnabled(HudWidgetRegistry.GasWorkers);
                    return UiText.Get(minerals == gas ? minerals ? "On" : "Off" : "Partially on");
                case FeatureSettingsDetailKind.AbilityStatus:
                    return UiText.Get("Per unit");
                case FeatureSettingsDetailKind.TransportCargo:
                    return UiText.Get(_actions.LayoutSnapshot.ShowTransportCargo ? "On" : "Off");
                default:
                    return UiText.Get(_actions.IsFeatureEnabled(featureKey) ? "On" : "Off");
            }
        }
    }
}
