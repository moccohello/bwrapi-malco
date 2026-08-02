using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Malco.Localization;
using Malco.Models;
using Malco.Data;
using Malco.Configuration.Models;
using Malco.Presentation.Hud.Tiles;
using Malco.Presentation.Hud.Units;

namespace Malco.Presentation.Hud.Buildings
{
    internal sealed class BuildingsPresenter
    {
        private readonly HudTileFactory _tileFactory;
        private long _sessionGeneration = -1;
        private string _semanticKey;

        public BuildingsPresenter(HudTileFactory tileFactory, Brush mutedBrush)
        {
            _tileFactory = tileFactory ?? throw new ArgumentNullException(nameof(tileFactory));
            View = UnitsPresenter.BuildView(mutedBrush ?? throw new ArgumentNullException(nameof(mutedBrush)));
        }

        public UnitHudViewHandles View { get; }

        public bool Apply(UnitHudPresentationInput input)
        {
            if (input.Preferences == null) throw new ArgumentException("Display preferences are required.", nameof(input));
            EnsureSession(input.SessionGeneration);
            var ownedRaces = input.Snapshot != null
                ? OwnedTechnologyRacePolicy.Resolve(
                    input.Snapshot.Race,
                    input.Snapshot.UnitCounts,
                    input.Snapshot.BuildingCounts)
                : new[] { Race.Unknown };
            var buildings = input.Snapshot != null && input.Snapshot.BuildingCounts != null
                ? input.Snapshot.BuildingCounts.Where(unit => unit != null &&
                    unit.Count > 0 &&
                    input.Preferences.IsItemShown(TechTreeItem.BuildingKey(unit.UnitId)))
                    .OrderBy(unit => TechTreeCatalog.GetDisplayOrder(ownedRaces, TechTreeItem.BuildingKey(unit.UnitId)))
                    .ThenBy(unit => unit.UnitId)
                    .ToList()
                : new List<UnitCount>();
            ApplyVisibility(buildings.Count, input.EditorMode);
            var iconSize = input.Preferences.BuildingIconSize;
            var semanticKey = iconSize + "|" + string.Join(",", ownedRaces) + "|" + UnitsPresenter.BuildSemanticKey(buildings);
            if (string.Equals(_semanticKey, semanticKey, StringComparison.Ordinal))
            {
                return buildings.Count > 0;
            }

            _semanticKey = semanticKey;
            View.Tiles.Children.Clear();
            var metrics = TileMetrics.FromWidth(
                MalcoPreferenceValues.IconTileWidth(iconSize),
                MalcoPreferenceValues.IconTileGap(iconSize));
            foreach (var building in buildings)
            {
                var displayName = UiText.GameName(building.Name);
                View.Tiles.Children.Add(_tileFactory.BuildImageTile(
                    _tileFactory.GetGrayscaleUnitIcon(building),
                    string.IsNullOrEmpty(displayName) ? "?" : displayName.Substring(0, 1).ToUpperInvariant(),
                    building.Count.ToString(CultureInfo.InvariantCulture),
                    displayName,
                    metrics,
                    false,
                    HudTileBadgeStyle.Count));
            }

            ApplyVisibility(buildings.Count, input.EditorMode);
            return buildings.Count > 0;
        }

        public void ResetSession(long generation)
        {
            _sessionGeneration = generation;
            _semanticKey = null;
            View.Tiles.Children.Clear();
            ApplyVisibility(0, false);
        }

        public void Invalidate() => _semanticKey = null;

        private void EnsureSession(long generation)
        {
            if (_sessionGeneration != generation) ResetSession(generation);
        }

        private void ApplyVisibility(int count, bool editorMode)
        {
            View.Tiles.Visibility = count == 0 ? Visibility.Collapsed : Visibility.Visible;
            View.EmptyText.Text = UiText.Get("No buildings");
            View.EmptyText.Visibility = editorMode && count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
