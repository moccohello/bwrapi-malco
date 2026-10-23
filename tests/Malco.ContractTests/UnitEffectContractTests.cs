using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BwrApi.Client;
using Malco;
using Malco.Configuration.Models;
using Malco.Data;
using Malco.Models;
using Malco.Presentation.Spatial;
using Xunit;

namespace Malco.ContractTests
{
    public sealed class UnitEffectContractTests
    {
        [Fact]
        public void EnemyUnitsNeedDetectionAndVisionWhileOwnUnitsAlwaysShow()
        {
            var runtime = Map(
                SdkUnit("own-ling", 0, 37, 100, 100) with { IsBurrowed = true, IsDetected = false },
                SdkUnit("enemy-ling", 1, 37, 200, 200) with { IsBurrowed = true, IsDetected = false },
                SdkUnit("enemy-detected-fog", 1, 37, 300, 300) with { IsBurrowed = true, IsDetected = true });

            Assert.True(runtime.Units.Single(unit => unit.UnitTag == "own-ling").IsVisibleToPerspective);
            Assert.False(runtime.Units.Single(unit => unit.UnitTag == "enemy-ling").IsVisibleToPerspective);
            // Without a known visibility grid the enemy is treated as unseen.
            Assert.False(runtime.Units.Single(unit => unit.UnitTag == "enemy-detected-fog").IsVisibleToPerspective);

            var states = new List<UnitSpatialState>();
            UnitEffectProjector.Append(states, runtime.Units, runtime.VisibleStorms);
            var group = Assert.Single(states);
            Assert.Equal(37, Assert.Single(group.BurrowedUnits).UnitId);
        }

        [Fact]
        public void BurrowedZergOnTheSameSpotShareOneCountedMarker()
        {
            var states = new List<UnitSpatialState>();
            UnitEffectProjector.Append(
                states,
                new[]
                {
                    Burrowed("a", 37, 64, 64),
                    Burrowed("b", 37, 64, 64),
                    Burrowed("c", 103, 64, 64),
                    Burrowed("d", 37, 96, 64),
                    Burrowed("mine", 13, 64, 64) // spider mines are not Zerg
                },
                null);

            Assert.Equal(2, states.Count);
            var stacked = states.Single(state => state.MapX == 64);
            Assert.Equal(new[] { "37x2", "103x1" }, stacked.BurrowedUnits.Select(item => item.UnitId + "x" + item.Count));
            Assert.Equal("burrow=37x2,103x1", SpatialPresenter.BuildBurrowToken(stacked, Preferences()));
        }

        [Theory]
        [InlineData(75, 250)]
        [InlineData(30, 100)]
        [InlineData(1, 4)]
        public void IrradiateReportsRemainingDamageAndTime(byte timer, int damage)
        {
            var unit = Runtime("u", 0, 0, 0);
            unit.IrradiateTimer = timer;
            var effect = Assert.Single(UnitEffectProjector.BuildEffects(unit));
            Assert.Equal("Irradiate", effect.Kind);
            Assert.Equal(damage, effect.RemainingDamage);
            Assert.Equal(timer * 8, effect.RemainingFrames);
        }

        [Fact]
        public void EffectsJoinAnExistingBadgeAndAddSpellAreas()
        {
            var vessel = new UnitSpatialState { UnitTag = "vessel", UnitId = 9, Energy = 120 };
            var states = new List<UnitSpatialState> { vessel };
            var lockedDown = Runtime("vessel", 9, 10, 10);
            lockedDown.LockdownTimer = 131;
            var swarm = Runtime("swarm", 202, 50, 50);
            swarm.HitPointsRaw = 0;
            swarm.RemoveTimer = 900;

            UnitEffectProjector.Append(
                states,
                new[] { lockedDown, swarm },
                new[] { new BwrApiRuntimeSpellArea { Tag = "s1", MapX = 5, MapY = 6, RemainingFrames = 40 } });

            Assert.Equal(3, states.Count);
            Assert.Equal("Lockdown", Assert.Single(vessel.Effects).Kind);
            Assert.Equal("fx=Dark Swarm:38", SpatialPresenter.BuildEffectToken(states.Single(state => state.UnitTag == "swarm"), Preferences()));
            Assert.Equal("fx=Psionic Storm:2:70", SpatialPresenter.BuildEffectToken(states.Single(state => state.UnitTag == "storm:s1"), Preferences()));
        }

        [Theory]
        [InlineData(60, 112)]
        [InlineData(57, 112)]
        [InlineData(56, 98)]
        [InlineData(8, 14)]
        [InlineData(0, 0)]
        public void StormRemainingDamageFollowsMeasuredChecks(int remainingFrames, int damage)
        {
            Assert.Equal(damage, UnitEffectProjector.StormRemainingDamage(remainingFrames));
        }

        [Fact]
        public void HiddenSpellsAndBurrowedUnitsAreLeftOutOfTheBadge()
        {
            var state = new UnitSpatialState
            {
                UnitTag = "u",
                Effects = { new UnitEffect { Kind = "Lockdown", RemainingFrames = 48 }, new UnitEffect { Kind = "Plague", RemainingFrames = 48 } },
                BurrowedUnits = { new CargoUnitCount { UnitId = 37, Count = 2 }, new CargoUnitCount { UnitId = 103, Count = 1 } }
            };
            var layout = HudLayoutConfig.CreateDefault();
            layout.HiddenSpellTimers.Add("Plague");
            layout.HiddenBurrowedUnitIds.Add(37);
            var preferences = HudDisplayPreferences.FromLayout(layout);

            Assert.Equal("fx=Lockdown:2", SpatialPresenter.BuildEffectToken(state, preferences));
            Assert.Equal("burrow=103x1", SpatialPresenter.BuildBurrowToken(state, preferences));
        }

        [Fact]
        public void IrradiateDamageIsShownOnlyForOrganicTargets()
        {
            var tank = Runtime("tank", 5, 0, 0);
            tank.IrradiateTimer = 75;
            Assert.Null(Assert.Single(UnitEffectProjector.BuildEffects(tank)).RemainingDamage);
        }

        [Fact]
        public void OlderInstalledPolicyLimitsRequireReinstall()
        {
            using var built = System.Text.Json.JsonDocument.Parse(
                "{\"maximum_payload_bytes\":33554432,\"maximum_envelope_bytes\":131072,\"feed_timeout_ms\":30000}");
            using var older = System.Text.Json.JsonDocument.Parse(
                "{\"maximum_payload_bytes\":13000000,\"maximum_envelope_bytes\":131072,\"feed_timeout_ms\":1}");
            Assert.True(Malco.Updates.LauncherRefreshNotice.IsBelow(older.RootElement, built.RootElement));
            Assert.False(Malco.Updates.LauncherRefreshNotice.IsBelow(built.RootElement, built.RootElement));
        }

        [Fact]
        public void SemanticKeyChangesOnlyWhenTheDisplayedSecondChanges()
        {
            string Key(int frames) => GameSnapshotSemanticKey.Build(SnapshotWith(new UnitSpatialState
            {
                UnitTag = "storm:s1",
                Effects = { new UnitEffect { Kind = "Psionic Storm", RemainingFrames = frames } }
            }));

            Assert.Equal(Key(48), Key(25));
            Assert.NotEqual(Key(48), Key(24));
        }

        private static BwrApiRuntimeUnit Burrowed(string tag, int unitId, int x, int y)
        {
            var unit = Runtime(tag, unitId, x, y);
            unit.IsBurrowed = true;
            return unit;
        }

        private static BwrApiRuntimeUnit Runtime(string tag, int unitId, int x, int y)
        {
            var type = BwapiBroodWarTables.GetUnitTypeInfo(unitId);
            return new BwrApiRuntimeUnit
            {
                UnitTag = tag,
                UnitId = unitId,
                Name = type.Name,
                IconKey = type.IconKey,
                HitPointsRaw = 2560,
                MapX = x,
                MapY = y,
                IsVisibleToPerspective = true
            };
        }

        private static HudDisplayPreferences Preferences()
        {
            var layout = HudLayoutConfig.CreateDefault();
            layout.ShowSpellTimers = true;
            layout.ShowBurrowedUnits = true;
            return HudDisplayPreferences.FromLayout(layout);
        }

        private static BwrApiRuntimeSnapshot Map(params BwrApiSemanticUnitV1[] units)
        {
            var source = (BwrApiSemanticSnapshotV1)typeof(BwrApiSemanticSnapshotV1)
                .GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)
                .Single()
                .Invoke(new object[]
                {
                    "test",
                    "unit-effects",
                    1UL,
                    10U,
                    BwrApiSemanticSnapshotStatus.Complete,
                    new BwrApiSemanticSessionV1(true, null, null, false),
                    (byte?)0,
                    null,
                    new[]
                    {
                        new BwrApiSemanticPlayerV1(0, 1, null, null, null, null, null, null),
                        new BwrApiSemanticPlayerV1(1, 1, null, null, null, null, null, null)
                    },
                    Array.Empty<BwrApiSemanticUnitCountV1>(),
                    units,
                    Array.Empty<BwrApiSemanticQueuedOrderV1>(),
                    Array.Empty<BwrApiSemanticUpgradeV1>(),
                    Array.Empty<BwrApiSemanticTechV1>(),
                    true,
                    string.Empty,
                    null,
                    null,
                    null
                });
            return new BwrApiSemanticRuntimeMapper().Map(
                new BwrApiFrameHeader(1, BwrApiDataState.Complete, BwrApiErrorKind.None, "unit-effects", 1, 1, 10),
                source);
        }

        private static BwrApiSemanticUnitV1 SdkUnit(string tag, byte owner, int unitTypeId, short x, short y)
        {
            var constructor = typeof(BwrApiSemanticUnitV1)
                .GetConstructors()
                .OrderByDescending(item => item.GetParameters().Length)
                .First();
            var values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["unitTag"] = tag,
                ["ownerId"] = owner,
                ["unitTypeId"] = (ushort)unitTypeId,
                ["x"] = x,
                ["y"] = y,
                ["hitPointsRaw"] = 2560,
                ["isCompleted"] = true,
                ["isHallucination"] = false
            };
            return (BwrApiSemanticUnitV1)constructor.Invoke(constructor.GetParameters()
                .Select(parameter => values.TryGetValue(parameter.Name, out var value)
                    ? value
                    : parameter.ParameterType.IsValueType
                        ? Activator.CreateInstance(parameter.ParameterType)
                        : null)
                .ToArray());
        }

        private static GameSnapshot SnapshotWith(UnitSpatialState state)
        {
            return new GameSnapshot(
                DateTime.UtcNow,
                true,
                Race.Zerg,
                0,
                0,
                0,
                0,
                0,
                string.Empty,
                Array.Empty<UnitCount>(),
                Array.Empty<UnitCount>(),
                Array.Empty<GasWorkerGroup>(),
                Array.Empty<MineralWorkerGroup>(),
                Array.Empty<UpgradeState>(),
                Array.Empty<UpgradeState>(),
                new[] { state });
        }
    }
}
