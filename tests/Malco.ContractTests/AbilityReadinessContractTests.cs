using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BwrApi.Client;
using Malco.Configuration.Models;
using Malco.Data;
using Malco.Models;
using Malco.Presentation.Spatial;
using Xunit;

namespace Malco.ContractTests
{
    public sealed class AbilityReadinessContractTests
    {
        [Fact]
        public void MapperAndProjectorPreserveUnavailableReadinessFacts()
        {
            var sourceUnit = SemanticUnit(
                8,
                energy: 25,
                completed: null,
                powered: null,
                interruptible: null,
                loaded: null,
                secondaryOrderId: null,
                lockdownTimer: null,
                maelstromTimer: null,
                stasisTimer: null,
                spiderMineCount: null);
            var runtime = Map(sourceUnit);
            var state = new UnitSpatialStateProjector()
                .Build(runtime.Units, runtime.CompletedUnitCounts)
                .Single();

            Assert.Null(runtime.Units.Single().IsCompletedObservation);
            Assert.Null(state.IsCompleted);
            Assert.Null(state.IsPowered);
            Assert.Null(state.IsInterruptible);
            Assert.Null(state.IsLoaded);
            Assert.Null(state.SecondaryOrderId);
            Assert.Null(state.LockdownTimer);
            Assert.Null(state.MaelstromTimer);
            Assert.Null(state.StasisTimer);
            Assert.Null(state.SpiderMineCount);
            Assert.Null(state.CompletedNuclearMissileCount);

            var frozen = FrozenSemanticSnapshot.Freeze(SnapshotWith(state));
            Assert.Null(frozen.UnitSpatialStates.Single().LockdownTimer);
            Assert.Null(frozen.UnitSpatialStates.Single().CompletedNuclearMissileCount);
        }

        [Fact]
        public void ScannerSweepRequiresConfirmedCompletionAndPower()
        {
            Assert.Equal(
                "ready=Scanner Sweep",
                Token(SemanticUnit(107, energy: 50, powered: true), 4, researched: false));
            Assert.Empty(Token(SemanticUnit(107, energy: 50, powered: false), 4, researched: false));
            Assert.Empty(Token(SemanticUnit(107, energy: 50, powered: null), 4, researched: false));
            Assert.Empty(Token(SemanticUnit(107, energy: 50, completed: null, powered: true), 4, researched: false));
        }

        [Fact]
        public void CasterReadinessRequiresKnownUnblockedCommandState()
        {
            Assert.Equal("ready=Cloaking Field", Token(SemanticUnit(8, energy: 25), 9));
            Assert.Empty(Token(SemanticUnit(8, energy: 25, completed: null), 9));
            Assert.Empty(Token(SemanticUnit(8, energy: 25, interruptible: null), 9));
            Assert.Empty(Token(SemanticUnit(8, energy: 25, loaded: null), 9));
            Assert.Empty(Token(SemanticUnit(8, energy: 25, lockdownTimer: null), 9));
            Assert.Empty(Token(SemanticUnit(8, energy: 25, maelstromTimer: 1), 9));
            Assert.Empty(Token(SemanticUnit(8, energy: 25, stasisTimer: null), 9));
            Assert.Empty(Token(SemanticUnit(8, energy: 25, orderId: 41), 9));
            Assert.Empty(Token(SemanticUnit(8, energy: 25, hitPointsRaw: 0), 9));
        }

        [Fact]
        public void CloakingTechIsSuppressedWhileCloakOrderIsActiveOrUnknown()
        {
            Assert.Equal("ready=Cloaking Field", Token(SemanticUnit(8, energy: 25, secondaryOrderId: 0), 9));
            Assert.Empty(Token(SemanticUnit(8, energy: 25, secondaryOrderId: 109), 9));
            Assert.Empty(Token(SemanticUnit(8, energy: 25, secondaryOrderId: null), 9));
        }

        [Fact]
        public void MineAndSiegeReadinessRequireAmmoAndSettledOrders()
        {
            Assert.Equal(
                "ready=Spider Mines",
                Token(SemanticUnit(2, energy: null, spiderMineCount: 3), 13));
            Assert.Empty(Token(SemanticUnit(2, energy: null, spiderMineCount: 0), 13));
            Assert.Empty(Token(SemanticUnit(2, energy: null, spiderMineCount: null), 13));
            Assert.Empty(Token(SemanticUnit(2, energy: null, spiderMineCount: 3), 13, researched: false));

            Assert.Equal("ready=Tank Siege Mode", Token(SemanticUnit(5, energy: null), 30));
            Assert.Empty(Token(SemanticUnit(5, energy: null, orderId: 98), 30));
            Assert.Empty(Token(SemanticUnit(5, energy: null, orderId: 99), 30));
        }

        [Fact]
        public void NuclearStrikeRequiresAConfirmedCompletedMissileCountWithoutEnergyOrResearch()
        {
            Assert.Equal(
                "ready=Nuclear Strike",
                Token(
                    SemanticUnit(1, energy: null),
                    45,
                    researched: false,
                    includeMissileCount: true,
                    missileCount: 1));
            Assert.Empty(Token(SemanticUnit(1, energy: null), 45));
            Assert.Empty(Token(
                SemanticUnit(1, energy: null),
                45,
                researched: false,
                includeMissileCount: true,
                missileCount: 0));
        }

        [Fact]
        public void ReadinessFactChangesInvalidateTheSemanticSnapshotKey()
        {
            var idle = new UnitSpatialState { UnitTag = "wraith", UnitId = 8, SecondaryOrderId = 0 };
            var cloaking = new UnitSpatialState { UnitTag = "wraith", UnitId = 8, SecondaryOrderId = 109 };

            Assert.NotEqual(
                GameSnapshotSemanticKey.Build(SnapshotWith(idle)),
                GameSnapshotSemanticKey.Build(SnapshotWith(cloaking)));
        }

        private static string Token(
            BwrApiSemanticUnitV1 unit,
            int techId,
            bool researched = true,
            bool includeMissileCount = false,
            uint missileCount = 0)
        {
            var counts = includeMissileCount
                ? new[] { new BwrApiSemanticUnitCountV1(0, 14, missileCount) }
                : Array.Empty<BwrApiSemanticUnitCountV1>();
            var runtime = Map(unit, counts);
            var state = new UnitSpatialStateProjector()
                .Build(runtime.Units, runtime.CompletedUnitCounts)
                .Single();
            var upgrades = researched
                ? new[] { new UpgradeState { StateKey = "tech:" + techId, IsComplete = true } }
                : Array.Empty<UpgradeState>();
            var snapshot = FrozenSemanticSnapshot.Freeze(SnapshotWith(state, upgrades));
            var layout = new HudLayoutConfig();
            layout.AbilityDisplayModes["unit:" + unit.UnitTypeId] = "tech:" + techId;
            return SpatialPresenter.BuildAbilityToken(
                snapshot,
                snapshot.UnitSpatialStates.Single(),
                HudDisplayPreferences.FromLayout(layout));
        }

        private static BwrApiRuntimeSnapshot Map(
            BwrApiSemanticUnitV1 unit,
            BwrApiSemanticUnitCountV1[] counts = null)
        {
            var source = (BwrApiSemanticSnapshotV1)typeof(BwrApiSemanticSnapshotV1)
                .GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)
                .Single()
                .Invoke(new object[]
                {
                    "test",
                    "ability-readiness",
                    1UL,
                    10U,
                    BwrApiSemanticSnapshotStatus.Complete,
                    new BwrApiSemanticSessionV1(true, null, null, false),
                    (byte?)0,
                    null,
                    new[] { new BwrApiSemanticPlayerV1(0, 1, null, null, null, null, null, null) },
                    counts ?? Array.Empty<BwrApiSemanticUnitCountV1>(),
                    new[] { unit },
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
                new BwrApiFrameHeader(
                    1,
                    BwrApiDataState.Complete,
                    BwrApiErrorKind.None,
                    "ability-readiness",
                    1,
                    1,
                    10),
                source);
        }

        private static BwrApiSemanticUnitV1 SemanticUnit(
            int unitId,
            int? energy,
            bool? completed = true,
            bool? powered = null,
            bool? interruptible = true,
            bool? loaded = false,
            byte orderId = 3,
            byte? secondaryOrderId = 0,
            byte? lockdownTimer = 0,
            byte? maelstromTimer = 0,
            byte? stasisTimer = 0,
            byte? spiderMineCount = null,
            int hitPointsRaw = 25600)
        {
            var constructor = typeof(BwrApiSemanticUnitV1)
                .GetConstructors()
                .OrderByDescending(item => item.GetParameters().Length)
                .First();
            var values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["unitTag"] = "unit-" + unitId,
                ["ownerId"] = (byte)0,
                ["unitTypeId"] = (ushort)unitId,
                ["hitPointsRaw"] = hitPointsRaw,
                ["isCompleted"] = completed,
                ["isHallucination"] = false,
                ["energyRaw"] = energy.HasValue ? (object)(ushort)(energy.Value * 256) : null,
                ["orderId"] = orderId,
                ["secondaryOrderId"] = secondaryOrderId,
                ["isPowered"] = powered,
                ["isLoaded"] = loaded
            };
            var unit = (BwrApiSemanticUnitV1)constructor.Invoke(constructor.GetParameters()
                .Select(parameter => values.TryGetValue(parameter.Name, out var value)
                    ? value
                    : parameter.ParameterType.IsValueType
                        ? Activator.CreateInstance(parameter.ParameterType)
                        : null)
                .ToArray());
            return unit with
            {
                IsInterruptible = interruptible,
                LockdownTimer = lockdownTimer,
                MaelstromTimer = maelstromTimer,
                StasisTimer = stasisTimer,
                SpiderMineCount = spiderMineCount
            };
        }

        private static GameSnapshot SnapshotWith(
            UnitSpatialState unit,
            IEnumerable<UpgradeState> upgrades = null)
        {
            return new GameSnapshot(
                DateTime.UtcNow,
                true,
                Race.Terran,
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
                upgrades ?? Array.Empty<UpgradeState>(),
                Array.Empty<UpgradeState>(),
                new[] { unit });
        }
    }
}
