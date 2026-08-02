using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Malco.Data;
using Malco.Configuration.Models;
using Malco.Localization;
using Malco.Models;
using Malco.Presentation.Hud.Tiles;

namespace Malco.Presentation.Hud.Units
{
    internal sealed class UnitsPresenter
    {
        private readonly HudTileFactory _tileFactory;
        private long _sessionGeneration = -1;
        private string _semanticKey;

        public UnitsPresenter(HudTileFactory tileFactory, Brush mutedBrush)
        {
            _tileFactory = tileFactory ?? throw new ArgumentNullException(nameof(tileFactory));
            View = BuildView(mutedBrush ?? throw new ArgumentNullException(nameof(mutedBrush)));
        }

        public UnitHudViewHandles View { get; }

        public bool Apply(UnitHudPresentationInput input)
        {
            if (input.Preferences == null) throw new ArgumentException("Display preferences are required.", nameof(input));
            EnsureSession(input.SessionGeneration);
            var units = new List<UnitCount>();
            var ownedRaces = input.Snapshot != null
                ? OwnedTechnologyRacePolicy.Resolve(
                    input.Snapshot.Race,
                    input.Snapshot.UnitCounts,
                    input.Snapshot.BuildingCounts)
                : new[] { Race.Unknown };
            if (input.Snapshot != null && input.Snapshot.UnitCounts != null)
            {
                units.AddRange(input.Snapshot.UnitCounts.Where(unit => unit != null &&
                    unit.Count > 0 &&
                    !BwapiBroodWarTables.IsWorkerUnitId(unit.UnitId) &&
                    !IsBuildingUnit(unit) &&
                    input.Preferences.IsItemShown(TechTreeItem.UnitKey(unit.UnitId)))
                    .OrderBy(unit => TechTreeCatalog.GetDisplayOrder(ownedRaces, TechTreeItem.UnitKey(unit.UnitId)))
                    .ThenBy(unit => unit.UnitId));
            }

            ApplyVisibility(units.Count, input.EditorMode, UiText.Get("No units"));
            var iconSize = input.Preferences.UnitIconSize;
            var semanticKey = iconSize + "|" + string.Join(",", ownedRaces) + "|" + BuildSemanticKey(units);
            if (string.Equals(_semanticKey, semanticKey, StringComparison.Ordinal))
            {
                return units.Count > 0;
            }

            _semanticKey = semanticKey;
            View.Tiles.Children.Clear();
            var metrics = TileMetrics.FromWidth(
                MalcoPreferenceValues.IconTileWidth(iconSize),
                MalcoPreferenceValues.IconTileGap(iconSize));
            foreach (var unit in units)
            {
                View.Tiles.Children.Add(BuildTile(unit, metrics));
            }

            ApplyVisibility(units.Count, input.EditorMode, UiText.Get("No units"));
            return units.Count > 0;
        }

        public void ResetSession(long generation)
        {
            _sessionGeneration = generation;
            _semanticKey = null;
            View.Tiles.Children.Clear();
            ApplyVisibility(0, false, UiText.Get("No units"));
        }

        public void Invalidate() => _semanticKey = null;

        private void EnsureSession(long generation)
        {
            if (_sessionGeneration != generation) ResetSession(generation);
        }

        private UIElement BuildTile(UnitCount unit, TileMetrics metrics)
        {
            var displayName = UiText.GameName(unit.Name);
            return _tileFactory.BuildImageTile(
                _tileFactory.GetGrayscaleUnitIcon(unit),
                string.IsNullOrEmpty(displayName) ? "?" : displayName.Substring(0, 1).ToUpperInvariant(),
                unit.Count > 0 ? unit.Count.ToString(CultureInfo.InvariantCulture) : string.Empty,
                displayName,
                metrics,
                false,
                HudTileBadgeStyle.Count);
        }

        private void ApplyVisibility(int count, bool editorMode, string emptyText)
        {
            View.Tiles.Visibility = count == 0 ? Visibility.Collapsed : Visibility.Visible;
            View.EmptyText.Text = emptyText;
            View.EmptyText.Visibility = editorMode && count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        internal static string BuildSemanticKey(IEnumerable<UnitCount> units) => string.Join(
            "|",
            (units ?? Array.Empty<UnitCount>())
                .Select(unit => unit.UnitId.ToString(CultureInfo.InvariantCulture) + ":" +
                                unit.Count.ToString(CultureInfo.InvariantCulture) + ":" +
                                (unit.IconKey ?? string.Empty)));

        private static bool IsBuildingUnit(UnitCount unit) =>
            unit != null && (unit.IsBuilding || BwapiBroodWarTables.IsKnownBuildingUnitId(unit.UnitId));

        internal static UnitHudViewHandles BuildView(Brush mutedBrush)
        {
            var body = new StackPanel { Margin = new Thickness(8d, 2d, 8d, 2d) };
            var tiles = new WrapPanel { Orientation = Orientation.Horizontal };
            var empty = new TextBlock
            {
                Text = string.Empty,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 13d,
                FontWeight = FontWeights.Medium,
                Foreground = mutedBrush,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 4d,
                    ShadowDepth = 1d,
                    Opacity = .85d
                }
            };
            body.Children.Add(tiles);
            body.Children.Add(empty);
            return new UnitHudViewHandles(body, tiles, empty);
        }
    }
}
