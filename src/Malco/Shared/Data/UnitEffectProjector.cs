using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Malco.Models;

namespace Malco.Data
{
    // Adds lasting-spell timers and burrowed Zerg markers for units the
    // perspective player can see. Status timers tick once every 8 frames;
    // spell units and bullets count down in frames.
    internal static class UnitEffectProjector
    {
        internal const int StatusTimerFrames = 8;
        internal const int IrradiateTotalDamage = 250;
        internal const int IrradiateTotalTicks = 75;
        private const int DarkSwarmUnitId = 202;
        private const int DisruptionWebUnitId = 105;

        public static void Append(
            List<UnitSpatialState> states,
            IEnumerable<BwrApiRuntimeUnit> units,
            IEnumerable<BwrApiRuntimeSpellArea> storms)
        {
            var byTag = states
                .Where(state => !string.IsNullOrEmpty(state.UnitTag))
                .ToDictionary(state => state.UnitTag, StringComparer.Ordinal);
            var burrowGroups = new Dictionary<Tuple<int, int, int>, UnitSpatialState>();
            foreach (var unit in (units ?? Enumerable.Empty<BwrApiRuntimeUnit>()).Where(IsPresent))
            {
                var effects = BuildEffects(unit);
                var position = RuntimeUnitCoordinates.Resolve(unit);
                if (unit.IsBurrowed == true && IsZerg(unit))
                {
                    // A burrowed caster keeps its own badge; other units stack per owner and spot.
                    var key = Tuple.Create(unit.OwnerId, position.Item1, position.Item2);
                    UnitSpatialState group;
                    if (!byTag.TryGetValue(unit.UnitTag, out group) && !burrowGroups.TryGetValue(key, out group))
                    {
                        group = NewState(
                            "burrow:" + unit.OwnerId.ToString(CultureInfo.InvariantCulture) + ":" +
                            position.Item1.ToString(CultureInfo.InvariantCulture) + ":" +
                            position.Item2.ToString(CultureInfo.InvariantCulture),
                            unit,
                            position);
                        burrowGroups[key] = group;
                        states.Add(group);
                    }
                    AddBurrowedUnit(group, unit);
                    MergeEffects(group, effects);
                    continue;
                }

                if (effects.Count == 0) continue;
                UnitSpatialState state;
                if (!byTag.TryGetValue(unit.UnitTag, out state))
                {
                    state = NewState(unit.UnitTag, unit, position);
                    byTag[unit.UnitTag] = state;
                    states.Add(state);
                }
                MergeEffects(state, effects);
            }

            foreach (var storm in storms ?? Enumerable.Empty<BwrApiRuntimeSpellArea>())
            {
                var state = new UnitSpatialState
                {
                    UnitTag = "storm:" + storm.Tag,
                    Name = "Psionic Storm",
                    MapX = storm.MapX,
                    MapY = storm.MapY
                };
                state.Effects.Add(new UnitEffect
                {
                    Kind = "Psionic Storm",
                    RemainingFrames = storm.RemainingFrames,
                    RemainingDamage = StormRemainingDamage(storm.RemainingFrames)
                });
                states.Add(state);
            }
        }

        // Rounded up so an active spell never reads 0s; the HUD counts 24 frames per second.
        public static int DisplaySeconds(UnitEffect effect) => (effect.RemainingFrames + 23) / 24;

        internal static List<UnitEffect> BuildEffects(BwrApiRuntimeUnit unit)
        {
            var effects = new List<UnitEffect>();
            AddStatus(effects, "Lockdown", unit.LockdownTimer);
            AddStatus(effects, "Stasis Field", unit.StasisTimer);
            AddStatus(effects, "Maelstrom", unit.MaelstromTimer);
            AddStatus(effects, "Defensive Matrix", unit.DefenseMatrixTimer);
            AddStatus(effects, "Ensnare", unit.EnsnareTimer);
            AddStatus(effects, "Plague", unit.PlagueTimer);
            if (unit.IrradiateTimer is byte irradiate && irradiate != 0)
            {
                effects.Add(new UnitEffect
                {
                    Kind = "Irradiate",
                    RemainingFrames = irradiate * StatusTimerFrames,
                    // Irradiate only damages organic units.
                    RemainingDamage = IsOrganic(unit)
                        ? (IrradiateTotalDamage * irradiate + IrradiateTotalTicks - 1) / IrradiateTotalTicks
                        : (int?)null
                });
            }
            if ((unit.UnitId == DarkSwarmUnitId || unit.UnitId == DisruptionWebUnitId) &&
                unit.RemoveTimer is ushort remove && remove != 0)
            {
                effects.Add(new UnitEffect
                {
                    Kind = unit.UnitId == DarkSwarmUnitId ? "Dark Swarm" : "Disruption Web",
                    RemainingFrames = remove
                });
            }
            return effects;
        }

        // A storm checks for damage whenever its timer is a multiple of 7
        // (56 down to 0, nine checks), and one unit takes at most 8 hits of 14.
        // This is the damage still ahead for a unit that stays inside.
        internal static int StormRemainingDamage(int remainingFrames)
        {
            const int hitInterval = 7, hitDamage = 14, maximumHits = 8, checks = 9;
            var checksLeft = remainingFrames <= 0 ? 0 : Math.Min(checks, (remainingFrames - 1) / hitInterval + 1);
            var hitsLeft = checksLeft == checks ? maximumHits : Math.Max(0, checksLeft - 1);
            return hitsLeft * hitDamage;
        }

        private static void AddStatus(List<UnitEffect> effects, string kind, byte? timer)
        {
            if (timer is byte ticks && ticks != 0)
                effects.Add(new UnitEffect { Kind = kind, RemainingFrames = ticks * StatusTimerFrames });
        }

        private static bool IsPresent(BwrApiRuntimeUnit unit) =>
            unit != null &&
            unit.IsVisibleToPerspective &&
            !string.IsNullOrEmpty(unit.UnitTag) &&
            unit.IsLoaded != true &&
            string.IsNullOrEmpty(unit.TransportUnitTag) &&
            (unit.HitPointsRaw > 0 || unit.UnitId == DarkSwarmUnitId || unit.UnitId == DisruptionWebUnitId);

        private static bool IsZerg(BwrApiRuntimeUnit unit) =>
            unit.IconKey != null && unit.IconKey.StartsWith("zerg/", StringComparison.Ordinal);

        // Marine, Ghost, SCV, Civilian, Firebat, Medic, Dark Templar, Zealot, High Templar.
        private static readonly HashSet<int> OrganicNonZergUnitIds = new HashSet<int> { 0, 1, 7, 15, 32, 34, 61, 65, 67 };

        private static bool IsOrganic(BwrApiRuntimeUnit unit) =>
            OrganicNonZergUnitIds.Contains(unit.UnitId) || (IsZerg(unit) && !unit.IsBuilding);

        private static UnitSpatialState NewState(string tag, BwrApiRuntimeUnit unit, Tuple<int, int> position) =>
            new UnitSpatialState
            {
                UnitTag = tag,
                UnitId = unit.UnitId,
                Name = unit.Name,
                IconKey = unit.IconKey,
                MapX = position.Item1,
                MapY = position.Item2,
                HitPointsRaw = unit.HitPointsRaw
            };

        private static void AddBurrowedUnit(UnitSpatialState group, BwrApiRuntimeUnit unit)
        {
            var entry = group.BurrowedUnits.FirstOrDefault(item => item.UnitId == unit.UnitId);
            if (entry == null)
            {
                group.BurrowedUnits.Add(new CargoUnitCount
                {
                    UnitId = unit.UnitId, Name = unit.Name, IconKey = unit.IconKey, Count = 1
                });
                group.BurrowedUnits.Sort((left, right) => left.UnitId.CompareTo(right.UnitId));
                return;
            }
            entry.Count++;
        }

        // Stacked units share one badge, so keep the longest timer of each spell.
        private static void MergeEffects(UnitSpatialState state, List<UnitEffect> effects)
        {
            foreach (var effect in effects)
            {
                var existing = state.Effects.FirstOrDefault(item =>
                    string.Equals(item.Kind, effect.Kind, StringComparison.Ordinal));
                if (existing == null)
                    state.Effects.Add(effect);
                else if (effect.RemainingFrames > existing.RemainingFrames)
                {
                    existing.RemainingFrames = effect.RemainingFrames;
                    existing.RemainingDamage = effect.RemainingDamage;
                }
            }
        }
    }
}
