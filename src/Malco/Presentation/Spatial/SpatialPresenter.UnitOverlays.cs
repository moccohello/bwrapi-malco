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
        private Dictionary<string, Tuple<UnitSpatialState, string>> BuildUnitOverlays(
            FrozenSemanticSnapshot snapshot,
            SpatialFeaturePreferences preferences)
        {
            var result = new Dictionary<string, Tuple<UnitSpatialState, string>>(StringComparer.Ordinal);
            _overlayPreferences = preferences.DisplayPreferences;
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
                var effectToken = BuildEffectToken(state, preferences.DisplayPreferences);
                var burrowToken = BuildBurrowToken(state, preferences.DisplayPreferences);
                if (string.IsNullOrEmpty(abilityToken) && string.IsNullOrEmpty(cargoToken) &&
                    string.IsNullOrEmpty(larvaToken) && string.IsNullOrEmpty(morphToken) &&
                    string.IsNullOrEmpty(effectToken) && string.IsNullOrEmpty(burrowToken)) continue;
                var localized = !string.IsNullOrEmpty(larvaToken) || !string.IsNullOrEmpty(morphToken) ||
                                !string.IsNullOrEmpty(effectToken) || !string.IsNullOrEmpty(burrowToken);
                result[state.UnitTag] = Tuple.Create(state, abilityToken + ";cargo=" + cargoToken +
                    (string.IsNullOrEmpty(larvaToken) ? string.Empty : ";" + larvaToken) +
                    (string.IsNullOrEmpty(morphToken) ? string.Empty : ";" + morphToken) +
                    (string.IsNullOrEmpty(effectToken) ? string.Empty : ";" + effectToken) +
                    (string.IsNullOrEmpty(burrowToken) ? string.Empty : ";" + burrowToken) +
                    (localized ? ";language=" + UiText.CurrentLanguage : string.Empty));
            }
            return result;
        }

        internal static string BuildEffectToken(UnitSpatialState state, HudDisplayPreferences preferences)
        {
            var effects = VisibleEffects(state, preferences).ToList();
            if (effects.Count == 0) return string.Empty;
            return "fx=" + string.Join(",", effects.Select(effect => effect.Kind + ":" +
                UnitEffectProjector.DisplaySeconds(effect).ToString(CultureInfo.InvariantCulture) +
                (effect.RemainingDamage.HasValue
                    ? ":" + effect.RemainingDamage.Value.ToString(CultureInfo.InvariantCulture)
                    : string.Empty)));
        }

        internal static string BuildBurrowToken(UnitSpatialState state, HudDisplayPreferences preferences)
        {
            var units = VisibleBurrowedUnits(state, preferences).ToList();
            if (units.Count == 0) return string.Empty;
            return "burrow=" + string.Join(",", units.Select(item =>
                item.UnitId.ToString(CultureInfo.InvariantCulture) + "x" +
                item.Count.ToString(CultureInfo.InvariantCulture)));
        }

        // The content key is built from these filtered lists, so a badge is
        // rebuilt whenever a per-item setting changes what it shows.
        private static IEnumerable<UnitEffect> VisibleEffects(UnitSpatialState state, HudDisplayPreferences preferences) =>
            (state?.Effects ?? Enumerable.Empty<UnitEffect>())
                .Where(effect => preferences != null && preferences.IsSpellTimerShown(effect.Kind))
                .OrderBy(effect => effect.Kind, StringComparer.Ordinal);

        private static IEnumerable<CargoUnitCount> VisibleBurrowedUnits(UnitSpatialState state, HudDisplayPreferences preferences) =>
            (state?.BurrowedUnits ?? Enumerable.Empty<CargoUnitCount>())
                .Where(item => preferences != null && preferences.IsBurrowedUnitShown(item.UnitId));

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
            // Same outlined icon as cargo and burrow badges so it sits quietly on the egg.
            return _icons?.GetCargoUnitIcon(new UnitCount
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
                    Source = GetMorphTargetIcon(target), Width = 22d, Height = 22d,
                    Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Center,
                    ToolTip = UiText.GameName(BwapiBroodWarTables.GetUnitTypeInfo(target).Name)
                });
            }

            if (contentKey.IndexOf(";burrow=", StringComparison.Ordinal) >= 0)
                stack.Children.Add(BuildUnitCountRow(VisibleBurrowedUnits(state, _overlayPreferences)));

            var effectMarker = contentKey.IndexOf(";fx=", StringComparison.Ordinal);
            if (effectMarker >= 0)
            {
                foreach (var effect in VisibleEffects(state, _overlayPreferences))
                    stack.Children.Add(BuildEffectRow(effect));
            }

            var abilityReadyOnly = contentKey.StartsWith("ready=", StringComparison.Ordinal) &&
                                   cargo.Count == 0 && larvaMarker < 0 && morphMarker < 0 && effectMarker < 0;
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

        private FrameworkElement BuildEffectRow(UnitEffect effect)
        {
            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                ToolTip = UiText.GameName(effect.Kind)
            };
            var icon = _icons?.GetUpgradeIcon(new UpgradeState { Name = effect.Kind });
            if (icon != null)
            {
                row.Children.Add(new Image
                {
                    Source = icon, Width = 16d, Height = 16d, Stretch = Stretch.Uniform,
                    VerticalAlignment = VerticalAlignment.Center
                });
            }
            var text = UnitEffectProjector.DisplaySeconds(effect).ToString(CultureInfo.InvariantCulture) + "s" +
                       (effect.RemainingDamage.HasValue
                           ? " · " + effect.RemainingDamage.Value.ToString(CultureInfo.InvariantCulture)
                           : string.Empty);
            row.Children.Add(new TextBlock
            {
                Text = text,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 11d,
                FontWeight = FontWeights.Bold,
                Foreground = _style.Text,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(2d, 0d, 0d, 0d),
                Effect = ResourceTextShadow()
            });
            return row;
        }

        private FrameworkElement BuildUnitCountRow(IEnumerable<CargoUnitCount> units)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            foreach (var item in units ?? Enumerable.Empty<CargoUnitCount>())
            {
                var source = _icons?.GetCargoUnitIcon(new UnitCount
                {
                    UnitId = item.UnitId, Name = item.Name, IconKey = item.IconKey, Count = item.Count
                });
                if (source != null)
                {
                    row.Children.Add(new Image
                    {
                        Source = source, Width = 22d, Height = 22d, Stretch = Stretch.Uniform,
                        ToolTip = UiText.GameName(item.Name)
                    });
                }
                if (item.Count > 1)
                {
                    row.Children.Add(new TextBlock
                    {
                        Text = "×" + item.Count.ToString(CultureInfo.InvariantCulture),
                        FontFamily = new FontFamily("Segoe UI"),
                        FontSize = 11d,
                        FontWeight = FontWeights.Bold,
                        Foreground = _style.Text,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0.5d, 0d, 1d, 0d),
                        Effect = ResourceTextShadow()
                    });
                }
            }
            return row;
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
