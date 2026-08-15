using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Malco.Configuration.Models;
using Malco.Localization;
using Malco.Settings.Contracts;

namespace Malco.Settings.Views
{
    internal sealed partial class FeatureSettingsView
    {
        private void RenderDetail()
        {
            _detailHost.Children.Clear();
            _itemListHost = null;
            _searchInput = null;
            var feature = HudFeatureCatalog.Find(_selectedFeatureKey);
            if (feature == null)
            {
                feature = HudFeatureCatalog.FirstFeature;
                _selectedFeatureKey = feature.Key;
            }

            if (feature.DetailKind == FeatureSettingsDetailKind.General)
            {
                _detailHost.Children.Add(BuildGeneralDetail());
                _detailScroll.ScrollToTop();
                return;
            }

            _detailHost.Children.Add(BuildFeatureHeader(
                feature,
                feature.DetailKind == FeatureSettingsDetailKind.Standard));
            if (feature.DetailKind == FeatureSettingsDetailKind.ResourceWorkers)
            {
                _detailHost.Children.Add(BuildResourceWorkerSettings());
                _detailScroll.ScrollToTop();
                return;
            }
            if (feature.DetailKind == FeatureSettingsDetailKind.TransportCargo)
            {
                _detailHost.Children.Add(BuildTransportCargoSettings());
                _detailScroll.ScrollToTop();
                return;
            }
            if (feature.DetailKind == FeatureSettingsDetailKind.AbilityStatus)
            {
                _detailHost.Children.Add(BuildAbilityStatusSettings());
                _detailScroll.ScrollToTop();
                return;
            }
            if (string.Equals(feature.Key, HudWidgetRegistry.Workers, StringComparison.OrdinalIgnoreCase))
            {
                _detailHost.Children.Add(BuildWorkerCountStyleSelector());
            }
            if (feature.SupportsIconSize)
            {
                _detailHost.Children.Add(BuildIconSizeSelector(feature.Key));
            }
            if (feature.ItemPolicy.SettingKind == FeatureItemSettingKind.CompletionAlert)
            {
                _detailHost.Children.Add(BuildCompletionModeSelector());
                if (string.Equals(
                        MalcoPreferenceValues.NormalizeCompletionMode(_actions.LayoutSnapshot.CompletionDisplayMode),
                        MalcoPreferenceValues.Countdown10Seconds,
                        StringComparison.Ordinal))
                {
                    _detailHost.Children.Add(BuildCompletionCountdownSelector());
                }
            }
            if (feature.ItemPolicy.HasItems)
            {
                _detailHost.Children.Add(BuildFilterTools(feature));
                _itemListHost = new StackPanel();
                _detailHost.Children.Add(_itemListHost);
                RefreshItemList(feature);
            }
            _detailScroll.ScrollToTop();
        }

        private FrameworkElement BuildFeatureHeader(
            FeatureSettingsDefinition feature,
            bool showFeatureSwitch = true)
        {
            var grid = new Grid { Margin = new Thickness(16d) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(_compactLayout ? 0d : 94d) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            if (!_compactLayout)
            {
                var preview =
                    _previewFactory.BuildFeaturePreview(feature.PreviewKind);
                grid.Children.Add(preview);
            }
            var copy = new StackPanel
            {
                Margin = new Thickness(16d, 4d, 20d, 0d),
                VerticalAlignment = VerticalAlignment.Center
            };
            copy.Children.Add(_palette.Text(feature.Title, 20d, FontWeights.Bold, _palette.TextBrush));
            var description = _palette.Text(feature.Description, 13d, FontWeights.Normal, _palette.MutedBrush);
            description.TextWrapping = TextWrapping.Wrap;
            description.Margin = new Thickness(0d, 4d, 0d, 0d);
            copy.Children.Add(description);
            if (!feature.IsSpatial)
            {
                var arrange = _chrome.ActionButton(UiText.Get("Arrange on screen"));
                arrange.Height = 44d;
                arrange.HorizontalAlignment = HorizontalAlignment.Left;
                arrange.Margin = new Thickness(0d, 12d, 0d, 0d);
                arrange.Click += (sender, args) => OpenLayout(feature.Key);
                copy.Children.Add(arrange);
            }
            Grid.SetColumn(copy, 1);
            grid.Children.Add(copy);

            if (showFeatureSwitch)
            {
                var enabled = _actions.IsFeatureEnabled(feature.Key);
                var switchName = UiText.Get("Show in game") + ": " + feature.Title;
                var switchHost = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(12d, 8d, 0d, 0d)
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
                        _actions.SetWidgetEnabled(feature.Key, next);
                        Refresh();
                        RestoreFocusAfterRender(switchName);
                    }));
                Grid.SetColumn(switchHost, 2);
                grid.Children.Add(switchHost);
            }

            var header = new Border
            {
                Background = _palette.SurfaceBrush,
                BorderBrush = _palette.SeparatorBrush,
                BorderThickness = new Thickness(0d, 0d, 0d, 1d),
                Child = grid
            };
            AutomationProperties.SetName(header, feature.Title);
            return header;
        }

    }
}
