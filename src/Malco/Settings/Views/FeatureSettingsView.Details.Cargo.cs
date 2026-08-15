using System.Windows;
using System.Windows.Controls;
using Malco.Localization;
using Malco.Settings.Contracts;

namespace Malco.Settings.Views
{
    internal sealed partial class FeatureSettingsView
    {
        private FrameworkElement BuildTransportCargoSettings()
        {
            var title = UiText.Get("Passenger icons and counts");
            var row = new Grid { Margin = new Thickness(16d) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(52d) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var icon = _previewFactory.BuildFeatureIcon(
                FeatureSettingsPreviewKind.Transport,
                40d);
            icon.VerticalAlignment = VerticalAlignment.Center;
            row.Children.Add(icon);

            var copy = new StackPanel
            {
                Margin = new Thickness(12d, 0d, 16d, 0d),
                VerticalAlignment = VerticalAlignment.Center
            };
            copy.Children.Add(_palette.Text(title, 14d, FontWeights.Bold, _palette.TextBrush));
            var description = _palette.Text(
                UiText.Get("Show passenger unit icons and counts inside loaded transports."),
                12d,
                FontWeights.Normal,
                _palette.MutedBrush);
            description.Margin = new Thickness(0d, 3d, 0d, 0d);
            description.TextWrapping = TextWrapping.Wrap;
            copy.Children.Add(description);
            Grid.SetColumn(copy, 1);
            row.Children.Add(copy);

            var enabled = _actions.LayoutSnapshot.ShowTransportCargo;
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
                    _actions.ApplyEdit(SettingsEdit.SetTransportCargoVisible(next));
                    _actions.RefreshPresenterViews();
                    _actions.RefreshSpatialPresentation();
                    _actions.RefreshVisibility();
                    Refresh();
                    RestoreFocusAfterRender(switchName);
                }));
            Grid.SetColumn(switchHost, 2);
            row.Children.Add(switchHost);

            return new Border
            {
                Margin = new Thickness(0d, 20d, 0d, 0d),
                Background = _palette.SurfaceBrush,
                BorderBrush = _palette.SeparatorBrush,
                BorderThickness = new Thickness(0d, 1d, 0d, 1d),
                Child = row
            };
        }
    }
}
