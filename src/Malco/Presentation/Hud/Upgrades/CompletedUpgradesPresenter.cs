using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Malco.Localization;
using Malco.Models;
using Malco.Data;
using Malco.Configuration.Models;
using Malco.Presentation.Hud.Tiles;

namespace Malco.Presentation.Hud.Upgrades
{
    internal sealed class CompletedUpgradesPresenter
    {
        private readonly UpgradeTileFactory _tiles;
        private string _semanticKey;

        public CompletedUpgradesPresenter(UpgradeTileFactory tiles, Brush mutedBrush)
        {
            _tiles = tiles ?? throw new ArgumentNullException(nameof(tiles));
            var body = new StackPanel { Margin = new Thickness(8d, 2d, 8d, 2d) };
            var tilePanel = new WrapPanel { Orientation = Orientation.Horizontal };
            var empty = new TextBlock
            {
                FontFamily = new FontFamily("Segoe UI"), FontSize = 13d, FontWeight = FontWeights.Medium,
                Foreground = mutedBrush ?? throw new ArgumentNullException(nameof(mutedBrush)),
                TextTrimming = TextTrimming.CharacterEllipsis,
                Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 4d, ShadowDepth = 1d, Opacity = .85d }
            };
            body.Children.Add(tilePanel);
            body.Children.Add(empty);
            View = new CompletedUpgradeViewHandles(body, tilePanel, empty);
        }

        public CompletedUpgradeViewHandles View { get; }

        public bool Apply(UpgradePresentationInput input, out IList<UpgradeState> allStates)
        {
            allStates = input.Snapshot != null && input.Snapshot.Upgrades != null
                ? input.Snapshot.Upgrades.Where(state => state != null && !string.IsNullOrEmpty(state.Name)).ToList()
                : new List<UpgradeState>();
            var ownedRaces = input.Snapshot != null
                ? OwnedTechnologyRacePolicy.Resolve(
                    input.Snapshot.Race,
                    input.Snapshot.UnitCounts,
                    input.Snapshot.BuildingCounts)
                : new[] { Race.Unknown };
            var states = allStates
                .Where(state => input.Preferences.IsItemShown(UpgradePresentationIdentity.ForState(state)) && state.IsComplete)
                .OrderBy(state => TechTreeCatalog.GetDisplayOrder(ownedRaces, UpgradePresentationIdentity.ForState(state)))
                .ThenBy(UpgradePresentationIdentity.ForState, StringComparer.OrdinalIgnoreCase)
                .ToList();
            ApplyVisibility(states.Count, input.EditorMode);
            var iconSize = input.Preferences.CompletedUpgradeIconSize;
            var key = iconSize + "|" + UpgradePresentationIdentity.BuildCompletedKey(states);
            if (!string.Equals(_semanticKey, key, StringComparison.Ordinal))
            {
                _semanticKey = key;
                View.Tiles.Children.Clear();
                var metrics = TileMetrics.FromWidth(
                    MalcoPreferenceValues.IconTileWidth(iconSize),
                    MalcoPreferenceValues.IconTileGap(iconSize));
                foreach (var state in states) View.Tiles.Children.Add(_tiles.BuildCompleted(state, metrics));
            }
            return states.Count > 0;
        }

        public void Reset()
        {
            _semanticKey = null;
            View.Tiles.Children.Clear();
            ApplyVisibility(0, false);
        }

        public void Invalidate() => _semanticKey = null;

        private void ApplyVisibility(int count, bool editorMode)
        {
            View.Tiles.Visibility = count == 0 ? Visibility.Collapsed : Visibility.Visible;
            View.EmptyText.Text = UiText.Get("No upgrades or tech");
            View.EmptyText.Visibility = editorMode && count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
