using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Malco.Data;
using Malco.Configuration.Models;
using Malco.Models;
using Malco.Presentation.Hud.Tiles;

namespace Malco.Presentation.Hud.Upgrades
{
    internal sealed class AvailableUpgradesPresenter
    {
        private readonly UpgradeTileFactory _tiles;
        private string _semanticKey;

        public AvailableUpgradesPresenter(UpgradeTileFactory tiles)
        {
            _tiles = tiles ?? throw new ArgumentNullException(nameof(tiles));
            var tilePanel = new WrapPanel { Orientation = Orientation.Horizontal };
            var panel = new Border
            {
                MinHeight = 44d,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(0d),
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0d),
                Child = tilePanel,
                IsHitTestVisible = false,
                Visibility = Visibility.Collapsed
            };
            View = new AvailableUpgradeViewHandles(panel, tilePanel);
        }

        public AvailableUpgradeViewHandles View { get; }

        public bool Apply(UpgradePresentationInput input)
        {
            var snapshot = input.Snapshot;
            if (!input.Preferences.IsWidgetEnabled(HudWidgetRegistry.AvailableUpgrades) ||
                snapshot == null || !snapshot.IsInMatch || snapshot.AvailableUpgrades == null)
            {
                Clear();
                return false;
            }
            var ownedRaces = OwnedTechnologyRacePolicy.Resolve(
                snapshot.Race,
                snapshot.UnitCounts,
                snapshot.BuildingCounts);
            var states = snapshot.AvailableUpgrades
                .Where(state => state != null &&
                    input.Preferences.IsAvailableAlertEnabled(UpgradePresentationIdentity.ForState(state)))
                .OrderBy(state => TechTreeCatalog.GetDisplayOrder(ownedRaces, UpgradePresentationIdentity.ForState(state)))
                .ThenBy(UpgradePresentationIdentity.ForState, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (states.Count == 0)
            {
                Clear();
                return false;
            }
            var iconSize = input.Preferences.AvailableUpgradeIconSize;
            var key = iconSize + "|" + UpgradePresentationIdentity.BuildAvailableKey(snapshot.Race, states);
            if (!string.Equals(_semanticKey, key, StringComparison.Ordinal))
            {
                _semanticKey = key;
                View.Tiles.Children.Clear();
                var metrics = TileMetrics.FromWidth(
                    MalcoPreferenceValues.IconTileWidth(iconSize),
                    MalcoPreferenceValues.IconTileGap(iconSize));
                foreach (var state in states) View.Tiles.Children.Add(_tiles.BuildAvailable(state, metrics));
            }
            View.Panel.Visibility = Visibility.Visible;
            return true;
        }

        public void Clear()
        {
            _semanticKey = null;
            View.Tiles.Children.Clear();
            View.Panel.Visibility = Visibility.Collapsed;
        }

        public void Invalidate() => _semanticKey = null;
    }
}
