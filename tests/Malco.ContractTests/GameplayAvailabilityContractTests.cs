using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BwrApi.Client;
using Malco.Data;
using Malco.Models;
using Xunit;

namespace Malco.ContractTests
{
    public sealed class GameplayAvailabilityContractTests
    {
        [Theory]
        [InlineData(false, false)]
        [InlineData(true, true)]
        [InlineData(null, true)]
        public void MapTechPermissionSurvivesPrerequisiteInference(bool? permission, bool expected)
        {
            var source = Source(Race.Terran,
                new[] { Unit(112, "academy") },
                techs: new[] { new BwrApiSemanticTechV1(0, 0, false, permission, false, 0) });
            var runtime = new BwrApiSemanticRuntimeMapper().Map(Header(), source);
            Assert.Equal(permission, runtime.TechAvailability[0]);
            Assert.Equal(expected, Snapshot(runtime).AvailableUpgrades.Any(item => item.StateKey == "tech:0"));
        }

        [Theory]
        [InlineData(0, 0, false)]
        [InlineData(1, 1, false)]
        [InlineData(1, 0, true)]
        [InlineData(null, 1, true)]
        public void MapUpgradeLimitSurvivesPrerequisiteInference(int? maxLevel, int level, bool expected)
        {
            var source = Source(Race.Terran,
                new[] { Unit(122, "engineering-bay"), Unit(116, "science-facility") },
                upgrades: new[] { new BwrApiSemanticUpgradeV1(0, 0, (byte)level,
                    maxLevel.HasValue ? (byte?)maxLevel.Value : null, false, 0) });
            var runtime = new BwrApiSemanticRuntimeMapper().Map(Header(), source);
            Assert.Equal(maxLevel, runtime.UpgradeMaxLevels[0]);
            Assert.Equal(expected, Snapshot(runtime).AvailableUpgrades.Any(item => item.StateKey == "upgrade:0"));
        }

        [Theory]
        [InlineData(false, true)]
        [InlineData(null, true)]
        [InlineData(true, false)]
        public void ForgeOpportunityDoesNotClaimUnconfirmedPowerIsReady(bool? powered, bool blocked)
        {
            var runtime = new BwrApiSemanticRuntimeMapper().Map(Header(), Source(Race.Protoss,
                new[] { Unit(166, "forge", powered: powered) }));
            Assert.Equal(powered, runtime.Units[0].IsPowered);
            Assert.Equal(blocked, Snapshot(runtime).AvailableUpgrades.Single(item => item.StateKey == "upgrade:5").IsBlocked);
        }

        [Theory]
        [InlineData(null, true)]
        [InlineData("different-shop", true)]
        [InlineData("shop", false)]
        public void MachineShopRequiresAttachmentToItsOwnCompletedFactory(string attachedTag, bool blocked)
        {
            var runtime = new BwrApiSemanticRuntimeMapper().Map(Header(), Source(Race.Terran,
                new[] { Unit(113, "factory", addon: attachedTag), Unit(120, "shop") }));
            Assert.Equal(attachedTag, runtime.Units[0].AddonUnitTag);
            Assert.Equal(blocked, Snapshot(runtime).AvailableUpgrades.Single(item => item.StateKey == "upgrade:17").IsBlocked);
        }

        [Fact]
        public void ASecondPoweredProducerKeepsOpportunityUsable()
        {
            var runtime = new BwrApiSemanticRuntimeMapper().Map(Header(), Source(Race.Protoss,
                new[] { Unit(166, "unpowered", powered: false), Unit(166, "powered", powered: true) }));
            Assert.False(Snapshot(runtime).AvailableUpgrades.Single(item => item.StateKey == "upgrade:5").IsBlocked);
        }

        [Theory]
        [InlineData(false, true)]
        [InlineData(true, false)]
        public void ForgeRequiresOneProducerThatIsBothPoweredAndFree(bool idlePowered, bool blocked)
        {
            var runtime = new BwrApiSemanticRuntimeMapper().Map(Header(), Source(Race.Protoss,
                new[]
                {
                    Unit(166, "busy-powered-forge", powered: true, upgrading: true),
                    Unit(166, "idle-forge", powered: idlePowered, upgrading: false)
                }, upgrades: new[] { new BwrApiSemanticUpgradeV1(0, 13, 0, 3, true, 24) }));
            Assert.True(runtime.Units[0].IsUpgrading);
            Assert.False(runtime.Units[1].IsUpgrading);
            Assert.Equal(blocked, Snapshot(runtime).AvailableUpgrades.Single(item => item.StateKey == "upgrade:5").IsBlocked);
        }

        [Theory]
        [InlineData(false, true)]
        [InlineData(true, false)]
        public void MachineShopRequiresOneProducerThatIsBothAttachedAndFree(bool idleAttached, bool blocked)
        {
            var runtime = new BwrApiSemanticRuntimeMapper().Map(Header(), Source(Race.Terran,
                new[]
                {
                    Unit(113, "busy-factory", addon: "busy-shop"),
                    Unit(120, "busy-shop", researching: true),
                    Unit(113, "idle-factory", addon: idleAttached ? "idle-shop" : null),
                    Unit(120, "idle-shop", researching: false)
                }, techs: new[] { new BwrApiSemanticTechV1(0, 3, false, true, true, 24) }));
            Assert.True(runtime.Units[1].IsResearching);
            Assert.False(runtime.Units[3].IsResearching);
            Assert.Equal(blocked, Snapshot(runtime).AvailableUpgrades.Single(item => item.StateKey == "upgrade:17").IsBlocked);
        }

        [Theory]
        [InlineData(75)]
        [InlineData(76)]
        public void ResearchAndUpgradeOrdersBlockProducerWhenOptionalActivityFactsAreUnknown(int order)
        {
            var runtime = new BwrApiSemanticRuntimeMapper().Map(Header(), Source(Race.Terran,
                new[] { Unit(112, "busy-academy", researching: null, upgrading: null, order: order) }));
            Assert.Null(runtime.Units[0].IsResearching);
            Assert.Null(runtime.Units[0].IsUpgrading);
            Assert.True(Snapshot(runtime).AvailableUpgrades.Single(item => item.StateKey == "upgrade:16").IsBlocked);
        }

        [Theory]
        [InlineData(null, false)]
        [InlineData(false, null)]
        public void UnconfirmedProducerActivityCannotBePresentedAsFree(bool? researching, bool? upgrading)
        {
            var runtime = new BwrApiSemanticRuntimeMapper().Map(Header(), Source(Race.Terran,
                new[] { Unit(112, "academy", researching: researching, upgrading: upgrading, order: 3) }));
            Assert.Equal(researching, runtime.Units[0].IsResearching);
            Assert.Equal(upgrading, runtime.Units[0].IsUpgrading);
            Assert.True(Snapshot(runtime).AvailableUpgrades.Single(item => item.StateKey == "upgrade:16").IsBlocked);
        }

        [Theory]
        [InlineData(1, true)]
        [InlineData(2, false)]
        public void LegacyCountFallbackStillDistinguishesAllBusyFromOneFreeProducer(int completed, bool blocked)
        {
            var states = UpgradeAvailabilityPolicy.BuildPrerequisiteAvailableUpgradeStates(Race.Protoss,
                new[] { new UnitCount { UnitId = 166, CompletedCount = completed, Count = completed, IsBuilding = true } },
                new[] { new UpgradeState { StateKey = "upgrade:13", IsInProgress = true, SecondsRemainingPrecise = 1 } });
            Assert.Equal(blocked, states.Single(item => item.StateKey == "upgrade:5").IsBlocked);
        }

        [Fact]
        public void HostLossRetainsObservationButChangesReadyStatusAndRecovers()
        {
            var coordinator = new ProviderPublicationCoordinator(new EmbeddedObserverMetrics());
            var source = Source(Race.Terran, new[] { Unit(112, "academy") });
            coordinator.PublishSemanticObservation(Header(), source);
            var ready = coordinator.GetProviderChannelState().Semantic;
            Assert.Equal(ProviderStatus.Ready, ready.Status);
            coordinator.PublishSemanticFailure("Embedded observer is waiting for a semantic frame", ProviderStatus.NotReady);
            var unavailable = coordinator.GetProviderChannelState().Semantic;
            Assert.Equal(ProviderStatus.NotReady, unavailable.Status);
            Assert.Same(ready.Snapshot, unavailable.Snapshot);
            Assert.Equal(ready.Sequence, unavailable.Sequence);
            coordinator.PublishSemanticObservation(Header(), source);
            Assert.Equal(ProviderStatus.Ready, coordinator.GetProviderChannelState().Semantic.Status);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(null)]
        public void MissingUnitListIsNeverPublishedAsReadyEmptyUnits(bool? unitsAvailable)
        {
            var coordinator = new ProviderPublicationCoordinator(new EmbeddedObserverMetrics());
            coordinator.PublishSemanticObservation(Header(), Source(Race.Terran, new[] { Unit(112, "academy") }));
            var ready = coordinator.GetProviderChannelState().Semantic;
            Assert.Equal(ProviderStatus.Ready, ready.Status);

            coordinator.PublishSemanticObservation(
                Header(),
                Source(Race.Terran, Array.Empty<BwrApiSemanticUnitV1>(), unitsAvailable: unitsAvailable));
            var missing = coordinator.GetProviderChannelState().Semantic;
            Assert.Equal(ProviderStatus.NotReady, missing.Status);
            Assert.Same(ready.Snapshot, missing.Snapshot);
        }

        private static GameSnapshot Snapshot(BwrApiRuntimeSnapshot runtime) =>
            new GameSnapshotMapper().BuildSemanticSnapshot(runtime, string.Empty);

        private static BwrApiFrameHeader Header() =>
            new BwrApiFrameHeader(1, BwrApiDataState.Complete, BwrApiErrorKind.None, "availability-test", 1, 1, 10);

        private static BwrApiSemanticSnapshotV1 Source(
            Race race,
            BwrApiSemanticUnitV1[] units,
            BwrApiSemanticUpgradeV1[] upgrades = null,
            BwrApiSemanticTechV1[] techs = null,
            bool? unitsAvailable = true)
        {
            byte raceId = race == Race.Terran ? (byte)1 : race == Race.Protoss ? (byte)2 : (byte)0;
            var source = (BwrApiSemanticSnapshotV1)typeof(BwrApiSemanticSnapshotV1)
                .GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance).Single()
                .Invoke(new object[] { "test", "availability-test", 1UL, 10U,
                    BwrApiSemanticSnapshotStatus.Complete,
                    new BwrApiSemanticSessionV1(true, null, null, false), (byte?)0, null,
                    new[] { new BwrApiSemanticPlayerV1(0, raceId, null, null, null, null, null, null) },
                    Array.Empty<BwrApiSemanticUnitCountV1>(), units,
                    Array.Empty<BwrApiSemanticQueuedOrderV1>(),
                    upgrades ?? Array.Empty<BwrApiSemanticUpgradeV1>(),
                    techs ?? Array.Empty<BwrApiSemanticTechV1>(), true, string.Empty,
                    null, null, null });
            // ABI8 publishes unit-list availability separately from Status.
            typeof(BwrApiSemanticSnapshotV1).GetProperty(nameof(BwrApiSemanticSnapshotV1.UnitsAvailable))
                .SetValue(source, unitsAvailable);
            return source;
        }

        private static BwrApiSemanticUnitV1 Unit(
            int id, string tag, bool? powered = null, string addon = null,
            bool? researching = false, bool? upgrading = false, int order = 3)
        {
            // The pinned SDK constructor includes nullable public facts.
            // Default unrelated fields without introducing a native/runtime fixture.
            var constructor = typeof(BwrApiSemanticUnitV1).GetConstructors().OrderByDescending(item => item.GetParameters().Length).First();
            var values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["unitTag"] = tag, ["unitTypeId"] = (ushort)id,
                ["isCompleted"] = true, ["isPowered"] = powered,
                ["addonUnitTag"] = addon,
                ["isResearching"] = researching, ["isUpgrading"] = upgrading,
                ["orderId"] = (byte)order
            };
            return (BwrApiSemanticUnitV1)constructor.Invoke(constructor.GetParameters().Select(parameter =>
                values.TryGetValue(parameter.Name, out var value) ? value :
                parameter.ParameterType.IsValueType ? Activator.CreateInstance(parameter.ParameterType) : null).ToArray());
        }
    }
}
