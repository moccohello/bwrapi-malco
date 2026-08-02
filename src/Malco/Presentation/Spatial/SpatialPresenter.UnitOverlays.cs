using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Malco.Configuration.Models;
using Malco.Data;
using Malco.Models;
using WpfEllipse = System.Windows.Shapes.Ellipse;

namespace Malco.Presentation.Spatial
{
    internal sealed partial class SpatialPresenter
    {
        private Dictionary<string, Tuple<UnitSpatialState, string>> BuildUnitOverlays(
            FrozenSemanticSnapshot snapshot,
            SpatialFeaturePreferences preferences)
        {
            var result = new Dictionary<string, Tuple<UnitSpatialState, string>>(StringComparer.Ordinal);
            if (snapshot == null) return result;
            foreach (var state in snapshot.UnitSpatialStates ?? Array.Empty<UnitSpatialState>())
            {
                if (state == null || string.IsNullOrWhiteSpace(state.UnitTag)) continue;
                var abilityToken = BuildAbilityToken(snapshot, state, preferences.DisplayPreferences);
                var cargoToken = preferences.DisplayPreferences != null &&
                                 preferences.DisplayPreferences.ShowTransportCargo
                    ? string.Join(",", (state.Cargo ?? new List<CargoUnitCount>())
                        .OrderBy(item => item.UnitId)
                        .Select(item => item.UnitId.ToString(CultureInfo.InvariantCulture) + "x" +
                                        item.Count.ToString(CultureInfo.InvariantCulture)))
                    : string.Empty;
                if (string.IsNullOrEmpty(abilityToken) && string.IsNullOrEmpty(cargoToken)) continue;
                result[state.UnitTag] = Tuple.Create(state, abilityToken + ";cargo=" + cargoToken);
            }
            return result;
        }

        private static string BuildAbilityToken(
            FrozenSemanticSnapshot snapshot,
            UnitSpatialState state,
            HudDisplayPreferences preferences)
        {
            if (preferences == null) return string.Empty;
            var mode = preferences.AbilityDisplayMode(state.UnitId);
            if (string.Equals(mode, MalcoPreferenceValues.AbilityEnergy, StringComparison.Ordinal))
                return state.Energy.HasValue
                    ? "energy=" + state.Energy.Value.ToString(CultureInfo.InvariantCulture)
                    : string.Empty;
            if (!mode.StartsWith("tech:", StringComparison.Ordinal)) return string.Empty;
            var unit = AbilityCatalog.Find(state.UnitId);
            var ability = unit?.Abilities.FirstOrDefault(item => string.Equals(item.Mode, mode, StringComparison.Ordinal));
            if (ability == null || !state.Energy.HasValue || state.Energy.Value < ability.EnergyCost) return string.Empty;
            if (ability.RequiresResearch && !(snapshot.Upgrades ?? Array.Empty<UpgradeState>()).Any(item =>
                item != null && item.IsComplete &&
                string.Equals(item.StateKey, ability.Mode, StringComparison.OrdinalIgnoreCase)))
                return string.Empty;
            return "ready=" + ability.Name;
        }

        private void AddUnitOverlay(
            string id,
            UnitSpatialState state,
            string contentKey,
            long sampleTimestamp)
        {
            var badge = new Border { ToolTip = state.Name };
            ApplyUnitOverlayContent(badge, state, contentKey);
            _tree.AddUnitOverlay(
                id,
                new UnitOverlaySpatialVisual(state, badge, contentKey, sampleTimestamp));
        }

        private void ApplyUnitOverlayContent(Border badge, UnitSpatialState state, string contentKey)
        {
            var stack = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            if (contentKey.StartsWith("energy=", StringComparison.Ordinal))
            {
                var end = contentKey.IndexOf(';');
                var energy = end > 7 ? contentKey.Substring(7, end - 7) : string.Empty;
                stack.Children.Add(new TextBlock
                {
                    Text = energy,
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 11d,
                    FontWeight = FontWeights.Bold,
                    Foreground = _style.Text,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Effect = ResourceTextShadow()
                });
            }
            else if (contentKey.StartsWith("ready=", StringComparison.Ordinal))
            {
                var end = contentKey.IndexOf(';');
                var ability = end > 6 ? contentKey.Substring(6, end - 6) : string.Empty;
                stack.Children.Add(BuildAbilityReadyIndicator(ability));
            }

            var cargoMarker = contentKey.IndexOf(";cargo=", StringComparison.Ordinal);
            var cargo = cargoMarker >= 0 && cargoMarker + 7 < contentKey.Length
                ? state.Cargo ?? new List<CargoUnitCount>()
                : new List<CargoUnitCount>();
            if (cargo.Count != 0)
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal };
                foreach (var item in cargo.OrderBy(item => item.UnitId))
                {
                    var entry = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0d, 0d, 0.5d, 0d) };
                    var source = _icons?.GetCargoUnitIcon(new UnitCount
                    {
                        UnitId = item.UnitId,
                        Name = item.Name,
                        IconKey = item.IconKey,
                        Count = item.Count
                    });
                    if (source != null)
                    {
                        entry.Children.Add(new Image
                        {
                            Source = source,
                            Width = 22d,
                            Height = 22d,
                            Stretch = Stretch.Uniform
                        });
                    }
                    entry.Children.Add(new TextBlock
                    {
                        Text = "×" + item.Count.ToString(CultureInfo.InvariantCulture),
                        FontFamily = new FontFamily("Segoe UI"),
                        FontSize = 10d,
                        FontWeight = FontWeights.Bold,
                        Foreground = _style.Text,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0.5d, 0d, 0d, 0d),
                        Opacity = 0.82d
                    });
                    row.Children.Add(entry);
                }
                stack.Children.Add(row);
            }

            var abilityReadyOnly = contentKey.StartsWith("ready=", StringComparison.Ordinal) && cargo.Count == 0;
            badge.Background = abilityReadyOnly
                ? Brushes.Transparent
                : cargo.Count != 0
                    ? TransportCargoOverlayBackground
                    : UnitOverlayBackground;
            badge.BorderBrush = abilityReadyOnly ? Brushes.Transparent : _style.ChipBorder;
            badge.BorderThickness = abilityReadyOnly ? new Thickness(0d) : new Thickness(0.5d);
            badge.CornerRadius = abilityReadyOnly ? new CornerRadius(0d) : new CornerRadius(2d);
            badge.Padding = abilityReadyOnly ? new Thickness(1d) : new Thickness(1d, 0.5d, 1d, 0.5d);
            badge.Effect = null;
            badge.Child = stack;
            badge.ToolTip = state.Name;
        }

        private static FrameworkElement BuildAbilityReadyIndicator(string ability)
        {
            var indicator = new Grid
            {
                Width = 14d,
                Height = 14d,
                ToolTip = ability
            };
            indicator.Children.Add(new WpfEllipse
            {
                Width = 12d,
                Height = 12d,
                Fill = AbilityReadyFill,
                Stroke = AbilityReadyStroke,
                StrokeThickness = 1.5d,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            });
            return indicator;
        }
    }
}
