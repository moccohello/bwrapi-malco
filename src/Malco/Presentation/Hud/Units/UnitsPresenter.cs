using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Malco.Configuration.Models;
using Malco.Data;
using Malco.Localization;
using Malco.Models;
using Malco.Presentation.Hud.Tiles;

namespace Malco.Presentation.Hud.Units
{
    internal enum UnitHudContent
    {
        Units,
        Buildings
    }

    internal abstract class UnitCountsPresenter
    {
        private readonly HudTileFactory _tileFactory;
        private readonly UnitHudContent _content;
        private long _sessionGeneration = -1;
        private string _semanticKey;

        protected UnitCountsPresenter(HudTileFactory tileFactory, Brush mutedBrush, UnitHudContent content)
        {
            _tileFactory = tileFactory ?? throw new ArgumentNullException(nameof(tileFactory));
            _content = content;
            View = BuildView(mutedBrush ?? throw new ArgumentNullException(nameof(mutedBrush)));
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
            var counts = input.Snapshot == null
                ? null
                : _content == UnitHudContent.Buildings
                    ? input.Snapshot.BuildingCounts
                    : input.Snapshot.UnitCounts;
            var items = counts == null
                ? new List<UnitCount>()
                : counts.Where(unit => ShouldDisplay(unit, input.Preferences))
                    .OrderBy(unit => TechTreeCatalog.GetDisplayOrder(ownedRaces, ItemKey(unit)))
                    .ThenBy(unit => unit.UnitId)
                    .ToList();

            ApplyVisibility(items.Count, input.EditorMode);
            var iconSize = _content == UnitHudContent.Buildings
                ? input.Preferences.BuildingIconSize
                : input.Preferences.UnitIconSize;
            var semanticKey = iconSize + "|" + string.Join(",", ownedRaces) + "|" + BuildSemanticKey(items);
            if (string.Equals(_semanticKey, semanticKey, StringComparison.Ordinal))
            {
                return items.Count > 0;
            }

            _semanticKey = semanticKey;
            View.Tiles.Children.Clear();
            var metrics = TileMetrics.FromWidth(
                MalcoPreferenceValues.IconTileWidth(iconSize),
                MalcoPreferenceValues.IconTileGap(iconSize));
            foreach (var item in items)
            {
                View.Tiles.Children.Add(BuildTile(item, metrics));
            }
            return items.Count > 0;
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

        private bool ShouldDisplay(UnitCount item, HudDisplayPreferences preferences)
        {
            if (item == null || item.Count <= 0) return false;
            return _content == UnitHudContent.Buildings
                ? preferences.IsItemShown(TechTreeItem.BuildingKey(item.UnitId))
                : !BwapiBroodWarTables.IsWorkerUnitId(item.UnitId) &&
                  !IsBuildingUnit(item) &&
                  preferences.IsItemShown(TechTreeItem.UnitKey(item.UnitId));
        }

        private string ItemKey(UnitCount item) => _content == UnitHudContent.Buildings
            ? TechTreeItem.BuildingKey(item.UnitId)
            : TechTreeItem.UnitKey(item.UnitId);

        private UIElement BuildTile(UnitCount item, TileMetrics metrics)
        {
            var displayName = UiText.GameName(item.Name);
            return _tileFactory.BuildImageTile(
                _tileFactory.GetGrayscaleUnitIcon(item),
                string.IsNullOrEmpty(displayName) ? "?" : displayName.Substring(0, 1).ToUpperInvariant(),
                item.Count.ToString(CultureInfo.InvariantCulture),
                displayName,
                metrics,
                false,
                HudTileBadgeStyle.Count);
        }

        private void ApplyVisibility(int count, bool editorMode)
        {
            View.Tiles.Visibility = count == 0 ? Visibility.Collapsed : Visibility.Visible;
            View.EmptyText.Text = UiText.Get(_content == UnitHudContent.Buildings ? "No buildings" : "No units");
            View.EmptyText.Visibility = editorMode && count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private static string BuildSemanticKey(IEnumerable<UnitCount> items) => string.Join(
            "|",
            items.Select(item => item.UnitId.ToString(CultureInfo.InvariantCulture) + ":" +
                                 item.Count.ToString(CultureInfo.InvariantCulture) + ":" +
                                 (item.IconKey ?? string.Empty)));

        private static bool IsBuildingUnit(UnitCount unit) =>
            unit.IsBuilding || BwapiBroodWarTables.IsKnownBuildingUnitId(unit.UnitId);

        private static UnitHudViewHandles BuildView(Brush mutedBrush)
        {
            var body = new StackPanel { Margin = new Thickness(8d, 2d, 8d, 2d) };
            var tiles = new WrapPanel { Orientation = Orientation.Horizontal };
            var empty = new TextBlock
            {
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

    internal sealed class UnitsPresenter : UnitCountsPresenter
    {
        public UnitsPresenter(HudTileFactory tileFactory, Brush mutedBrush)
            : base(tileFactory, mutedBrush, UnitHudContent.Units)
        {
        }
    }
}
