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
using WpfEllipse = System.Windows.Shapes.Ellipse;

namespace Malco.Presentation.Spatial
{
    internal sealed partial class SpatialPresenter
    {
        private static readonly SolidColorBrush MorphTargetBackground = FrozenBrush(Color.FromArgb(220, 9, 12, 18));
        private static readonly SolidColorBrush MorphTargetBorder = FrozenBrush(Color.FromRgb(208, 208, 208));
        private static readonly DropShadowEffect MorphTargetShadow = CreateMorphTargetShadow();

        private static DropShadowEffect CreateMorphTargetShadow()
        {
            var shadow = new DropShadowEffect
            {
                Color = Colors.Black, Opacity = 0.9d, BlurRadius = 5d, ShadowDepth = 1d
            };
            shadow.Freeze();
            return shadow;
        }

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
                var larvaToken = BuildLarvaToken(state, preferences.DisplayPreferences);
                var morphToken = BuildMorphTargetToken(state, preferences.DisplayPreferences);
                if (!string.IsNullOrEmpty(morphToken) && GetMorphTargetIcon(state.MorphTargetUnitId.Value) == null)
                    morphToken = string.Empty;
                var cargoToken = preferences.DisplayPreferences != null &&
                                 preferences.DisplayPreferences.ShowTransportCargo
                    ? string.Join(",", (state.Cargo ?? new List<CargoUnitCount>())
                        .OrderBy(item => item.UnitId)
                        .Select(item => item.UnitId.ToString(CultureInfo.InvariantCulture) + "x" +
                                        item.Count.ToString(CultureInfo.InvariantCulture)))
                    : string.Empty;
                if (string.IsNullOrEmpty(abilityToken) && string.IsNullOrEmpty(cargoToken) &&
                    string.IsNullOrEmpty(larvaToken) && string.IsNullOrEmpty(morphToken)) continue;
                result[state.UnitTag] = Tuple.Create(state, abilityToken + ";cargo=" + cargoToken +
                    (string.IsNullOrEmpty(larvaToken) ? string.Empty : ";" + larvaToken) +
                    (string.IsNullOrEmpty(morphToken) ? string.Empty : ";" + morphToken) +
                    (string.IsNullOrEmpty(larvaToken) && string.IsNullOrEmpty(morphToken)
                        ? string.Empty : ";language=" + UiText.CurrentLanguage));
            }
            return result;
        }

        internal static string BuildMorphTargetToken(UnitSpatialState state, HudDisplayPreferences preferences)
        {
            if (preferences?.ShowMorphTargetIcons != true || state == null || state.HitPointsRaw <= 0 ||
                !state.MorphTargetUnitId.HasValue) return string.Empty;
            var target = state.MorphTargetUnitId.Value;
            var valid = state.UnitId == 36
                ? target == 37 || target == 38 || target == 39 || target == 41 || target == 42 ||
                  target == 43 || target == 45 || target == 46 || target == 47
                : state.UnitId == 59 && (target == 44 || target == 62);
            return valid
                ? "morph=" + target.ToString(CultureInfo.InvariantCulture) : string.Empty;
        }

        private ImageSource GetMorphTargetIcon(int target)
        {
            var type = BwapiBroodWarTables.GetUnitTypeInfo(target);
            return _icons?.GetUnitIcon(new UnitCount
            {
                UnitId = target, Name = type.Name, IconKey = type.IconKey
            });
        }

        internal static string BuildLarvaToken(UnitSpatialState state, HudDisplayPreferences preferences)
        {
            if (preferences == null || !preferences.ShowLarvaCountdown || state == null ||
                state.HitPointsRaw <= 0 ||
                (state.UnitId != 131 && state.UnitId != 132 && state.UnitId != 133) ||
                !state.LarvaRemainingFrames.HasValue || state.LarvaRemainingFrames.Value == 0 ||
                state.ConnectedLarvaCount >= 3)
                return string.Empty;

            // Round up the observed game frames; presentation time must not advance this countdown.
            var seconds = (state.LarvaRemainingFrames.Value + 23UL) / 24UL;
            return "larva=" + seconds.ToString(CultureInfo.InvariantCulture);
        }

        internal static string BuildAbilityToken(
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
            if (ability == null || !HasObservedCasterEligibility(state)) return string.Empty;
            if (ability.EnergyCost > 0 &&
                (!state.Energy.HasValue || state.Energy.Value < ability.EnergyCost))
                return string.Empty;
            if (ability.RequiresResearch && !(snapshot.Upgrades ?? Array.Empty<UpgradeState>()).Any(item =>
                item != null && item.IsComplete &&
                string.Equals(item.StateKey, ability.Mode, StringComparison.OrdinalIgnoreCase)))
                return string.Empty;
            if (ability.TechId == 9 || ability.TechId == 10)
            {
                if (!state.SecondaryOrderId.HasValue || state.SecondaryOrderId.Value == 109)
                    return string.Empty;
            }
            if (ability.TechId == 13 &&
                (!state.SpiderMineCount.HasValue || state.SpiderMineCount.Value == 0))
                return string.Empty;
            if (ability.TechId == 30 && (state.OrderId == 98 || state.OrderId == 99))
                return string.Empty;
            if (ability.TechId == 45 &&
                (!state.CompletedNuclearMissileCount.HasValue ||
                 state.CompletedNuclearMissileCount.Value == 0))
                return string.Empty;
            return "ready=" + ability.Name;
        }

        private static bool HasObservedCasterEligibility(UnitSpatialState state)
        {
            if (state == null ||
                state.HitPointsRaw <= 0 ||
                state.IsCompleted != true ||
                state.IsLoaded != false ||
                state.OrderId == 41 ||
                state.LockdownTimer != 0 ||
                state.MaelstromTimer != 0 ||
                state.StasisTimer != 0)
                return false;

            if (state.IsPowered == false ||
                (state.IsBuilding && state.IsPowered != true))
                return false;

            return state.IsBuilding || state.IsInterruptible == true;
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
            var cargoEnd = cargoMarker >= 0 ? contentKey.IndexOf(';', cargoMarker + 7) : -1;
            if (cargoEnd < 0) cargoEnd = contentKey.Length;
            var cargo = cargoMarker >= 0 && cargoMarker + 7 < cargoEnd
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

            var larvaMarker = contentKey.IndexOf(";larva=", StringComparison.Ordinal);
            if (larvaMarker >= 0)
            {
                var larvaEnd = contentKey.IndexOf(';', larvaMarker + 7);
                if (larvaEnd < 0) larvaEnd = contentKey.Length;
                stack.Children.Add(new TextBlock
                {
                    Text = contentKey.Substring(larvaMarker + 7, larvaEnd - larvaMarker - 7) + "s",
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 11d,
                    FontWeight = FontWeights.Bold,
                    Foreground = _style.Text,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Effect = ResourceTextShadow()
                });
            }

            var morphMarker = contentKey.IndexOf(";morph=", StringComparison.Ordinal);
            if (morphMarker >= 0 && state.MorphTargetUnitId.HasValue)
            {
                var target = state.MorphTargetUnitId.Value;
                stack.Children.Add(new Image
                {
                    Source = GetMorphTargetIcon(target), Width = 28d, Height = 28d,
                    Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Center,
                    ToolTip = UiText.GameName(BwapiBroodWarTables.GetUnitTypeInfo(target).Name)
                });
            }

            var abilityReadyOnly = contentKey.StartsWith("ready=", StringComparison.Ordinal) &&
                                   cargo.Count == 0 && larvaMarker < 0 && morphMarker < 0;
            badge.Background = morphMarker >= 0 ? MorphTargetBackground : abilityReadyOnly
                ? Brushes.Transparent
                : cargo.Count != 0
                    ? TransportCargoOverlayBackground
                    : UnitOverlayBackground;
            badge.BorderBrush = morphMarker >= 0 ? MorphTargetBorder : abilityReadyOnly ? Brushes.Transparent : _style.ChipBorder;
            badge.BorderThickness = morphMarker >= 0 ? new Thickness(1.5d) : abilityReadyOnly ? new Thickness(0d) : new Thickness(0.5d);
            badge.CornerRadius = morphMarker >= 0 ? new CornerRadius(4d) : abilityReadyOnly ? new CornerRadius(0d) : new CornerRadius(2d);
            badge.Padding = morphMarker >= 0 ? new Thickness(2d) : abilityReadyOnly ? new Thickness(1d) : new Thickness(1d, 0.5d, 1d, 0.5d);
            badge.Effect = morphMarker >= 0 ? MorphTargetShadow : null;
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
