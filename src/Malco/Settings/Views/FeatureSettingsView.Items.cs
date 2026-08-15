using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
        private FrameworkElement BuildFilterTools(FeatureSettingsDefinition feature)
        {
            var race = BuildFilterGroup(UiText.Get("Race"), BuildRaceSelector());
            var search = BuildFilterGroup(UiText.Get("Search items"), BuildSearch(feature));
            search.HorizontalAlignment = _compactLayout
                ? HorizontalAlignment.Stretch
                : HorizontalAlignment.Right;
            if (_compactLayout)
            {
                var compact = new StackPanel { Margin = new Thickness(0d, 20d, 0d, 12d) };
                compact.Children.Add(race);
                search.Margin = new Thickness(0d, 12d, 0d, 0d);
                compact.Children.Add(search);
                return compact;
            }

            var tools = new Grid { Margin = new Thickness(0d, 20d, 0d, 12d) };
            tools.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            tools.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
            tools.Children.Add(race);
            Grid.SetColumn(search, 1);
            tools.Children.Add(search);
            return tools;
        }

        private FrameworkElement BuildFilterGroup(string label, FrameworkElement content)
        {
            var group = new StackPanel();
            var heading = _palette.Text(label, 12d, FontWeights.SemiBold, _palette.MutedBrush);
            heading.Margin = new Thickness(0d, 0d, 0d, 6d);
            group.Children.Add(heading);
            group.Children.Add(content);
            AutomationProperties.SetName(group, label);
            return group;
        }

        private FrameworkElement BuildRaceSelector()
        {
            var races = new StackPanel { Orientation = Orientation.Horizontal };
            foreach (var catalog in TechTreeCatalog.All)
            {
                var selected = catalog.Race == _actions.SelectedTechTreeRace;
                var label = UiText.Get(catalog.Name);
                var button = SegmentButton(label, selected, "settings-race");
                button.MinWidth = _ultraCompactLayout ? 60d : 78d;
                button.Click += (sender, args) =>
                {
                    _actions.SelectedTechTreeRace = catalog.Race;
                    RenderDetail();
                    RestoreFocusAfterRender(label);
                };
                races.Children.Add(button);
            }
            return races;
        }

        private FrameworkElement BuildSearch(FeatureSettingsDefinition feature)
        {
            var row = new Grid
            {
                Width = _compactLayout ? double.NaN : 300d,
                MinWidth = _ultraCompactLayout ? 120d : _compactLayout ? 180d : 300d,
                HorizontalAlignment = _compactLayout ? HorizontalAlignment.Stretch : HorizontalAlignment.Right,
                Margin = _compactLayout ? new Thickness(0d) : new Thickness(16d, 0d, 0d, 0d)
            };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
            var clearColumn = new ColumnDefinition
            {
                Width = new GridLength(string.IsNullOrEmpty(_searchText) ? 0d : 44d)
            };
            row.ColumnDefinitions.Add(clearColumn);
            var input = new TextBox
            {
                Text = _searchText,
                Height = 44d,
                Padding = new Thickness(10d, 7d, 8d, 0d),
                Foreground = _palette.TextBrush,
                Background = _palette.SurfaceBrush,
                BorderBrush = _palette.BorderBrush,
                BorderThickness = string.IsNullOrEmpty(_searchText)
                    ? new Thickness(1d)
                    : new Thickness(1d, 1d, 0d, 1d),
                FontSize = 13d,
                Style = FeatureSettingsStyles.CreateTextBox()
            };
            AutomationProperties.SetName(input, UiText.Get("Search") + ": " + feature.Title);
            _searchInput = input;
            row.Children.Add(input);
            var clear = _chrome.ActionButton("\u2715");
            clear.Width = 44d;
            clear.MinWidth = 44d;
            clear.Height = 44d;
            clear.Margin = new Thickness(0d);
            clear.Padding = new Thickness(0d);
            clear.ToolTip = UiText.Get("Clear search");
            clear.Click += (sender, args) =>
            {
                input.Clear();
                input.Focus();
            };
            AutomationProperties.SetName(clear, UiText.Get("Clear search"));
            clear.Visibility = string.IsNullOrEmpty(_searchText) ? Visibility.Collapsed : Visibility.Visible;
            input.TextChanged += (sender, args) =>
            {
                _searchText = input.Text ?? string.Empty;
                var empty = string.IsNullOrEmpty(_searchText);
                clear.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
                clearColumn.Width = new GridLength(empty ? 0d : 44d);
                input.BorderThickness = empty
                    ? new Thickness(1d)
                    : new Thickness(1d, 1d, 0d, 1d);
                RefreshItemList(feature);
            };
            Grid.SetColumn(clear, 1);
            row.Children.Add(clear);
            return row;
        }

        private void RefreshItemList(FeatureSettingsDefinition feature)
        {
            if (_itemListHost == null)
            {
                return;
            }
            _itemListHost.Children.Clear();
            var catalog = TechTreeCatalog.All.FirstOrDefault(candidate => candidate.Race == _actions.SelectedTechTreeRace)
                          ?? TechTreeCatalog.All.First();
            var featureItems = feature.ItemPolicy.SelectItems(catalog).ToList();
            var items = featureItems
                .Where(item => IsSearchMatch(item, _searchText))
                .ToList();
            var layout = _actions.LayoutSnapshot;
            _itemListHost.Children.Add(BuildItemListHeader(feature, featureItems));
            if (items.Count == 0)
            {
                AutomationProperties.SetName(_itemListHost, UiText.Get("No matching items."));
                AutomationProperties.SetLiveSetting(_itemListHost, AutomationLiveSetting.Polite);
                _itemListHost.Children.Add(BuildEmptyDetail(UiText.Get("No matching items.")));
                return;
            }
            AutomationProperties.SetName(
                _itemListHost,
                items.Count.ToString(CultureInfo.CurrentCulture) + " " + UiText.Get("results"));
            AutomationProperties.SetLiveSetting(_itemListHost, AutomationLiveSetting.Polite);
            foreach (var item in items)
            {
                _itemListHost.Children.Add(BuildItemRow(feature, item, layout));
            }
        }

        private FrameworkElement BuildItemListHeader(
            FeatureSettingsDefinition feature,
            IList<TechTreeItem> items)
        {
            var header = new Grid { Margin = new Thickness(4d, 0d, 4d, 8d) };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var heading = _palette.Text(
                feature.ItemPolicy.SectionTitle,
                12d,
                FontWeights.Bold,
                _palette.MutedBrush);
            heading.VerticalAlignment = VerticalAlignment.Center;
            header.Children.Add(heading);

            var actions = new StackPanel { Orientation = Orientation.Horizontal };
            actions.Children.Add(BuildBulkItemButton(feature, items, false));
            var allOn = BuildBulkItemButton(feature, items, true);
            allOn.Margin = new Thickness(8d, 0d, 0d, 0d);
            actions.Children.Add(allOn);
            Grid.SetColumn(actions, 1);
            header.Children.Add(actions);
            return header;
        }

        private Button BuildBulkItemButton(
            FeatureSettingsDefinition feature,
            IList<TechTreeItem> items,
            bool enabled)
        {
            var label = UiText.Get(enabled ? "All on" : "All off");
            var button = _chrome.ActionButton(label);
            button.MinWidth = 84d;
            button.Height = 34d;
            button.Padding = new Thickness(12d, 0d, 12d, 0d);
            button.IsEnabled = items != null && items.Count != 0;
            AutomationProperties.SetName(button, label);
            button.Click += (sender, args) =>
            {
                if (!_actions.ApplyEdit(feature.ItemPolicy.CreateBulkDelta(items, enabled)).Changed)
                {
                    return;
                }
                _actions.RefreshPresenterViews();
                _actions.RefreshVisibility();
                RefreshItemList(feature);
                RestoreFocusAfterRender(label);
            };
            return button;
        }

        private FrameworkElement BuildItemRow(
            FeatureSettingsDefinition feature,
            TechTreeItem item,
            HudLayoutSnapshot layout)
        {
            var grid = new Grid { Margin = new Thickness(12d, 0d, 12d, 0d) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44d) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.Children.Add(_previewFactory.BuildCatalogIcon(item, 32d));
            var displayName = UiText.GameName(item.Name);
            var name = _palette.Text(displayName, 13d, FontWeights.SemiBold, _palette.TextBrush);
            name.VerticalAlignment = VerticalAlignment.Center;
            name.TextTrimming = TextTrimming.CharacterEllipsis;
            Grid.SetColumn(name, 1);
            grid.Children.Add(name);
            var enabled = feature.ItemPolicy.ReadValue(layout, item);
            var toggle = BuildSwitch(
                feature.ItemPolicy.SettingLabel + ": " + displayName,
                enabled,
                next =>
                {
                    _actions.ApplyEdit(feature.ItemPolicy.CreateItemDelta(item, next));
                    _actions.RefreshPresenterViews();
                    _actions.RefreshVisibility();
                });
            toggle.Margin = new Thickness(12d, 0d, 0d, 0d);
            Grid.SetColumn(toggle, 2);
            grid.Children.Add(toggle);
            var row = new Border
            {
                MinHeight = 56d,
                Background = Brushes.Transparent,
                BorderBrush = _palette.SeparatorBrush,
                BorderThickness = new Thickness(0d, 0d, 0d, 1d),
                Child = grid
            };
            AutomationProperties.SetName(row, displayName);
            return row;
        }

    }
}
