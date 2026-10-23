using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BwrApi.Client;
using Malco;
using Malco.Configuration;
using Malco.Configuration.Models;
using Malco.Data;
using Malco.Models;
using Malco.Presentation.Spatial;
using Malco.Settings.Contracts;
using Malco.Settings.Controller;
using Xunit;

namespace Malco.ContractTests
{
    public sealed class LarvaCountdownContractTests
    {
        [Fact]
        public void PinnedSdkExposesNullableLarvaCountdownAndParentFacts()
        {
            var sdkAssembly = typeof(BwrApiSemanticUnitV1).Assembly;
            Assert.Equal(
                "5.0.0-embedded-preview.78",
                sdkAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);
            Assert.Equal(typeof(uint?), typeof(BwrApiSemanticUnitV1).GetProperty("RemainingTrainFrames").PropertyType);
            Assert.Equal(typeof(bool?), typeof(BwrApiSemanticUnitV1).GetProperty("HasHatcheryParent").PropertyType);
            Assert.Equal(typeof(string), typeof(BwrApiSemanticUnitV1).GetProperty("HatcheryUnitTag").PropertyType);
        }

        [Fact]
        public void MapsAllHatcheryTypesAndCountsLiveLarvaeForEachParent()
        {
            var projected = Project(
                Unit(131, "hatchery", remainingTrainFrames: 73),
                Unit(132, "lair", remainingTrainFrames: 41),
                Unit(133, "hive", remainingTrainFrames: 19),
                Unit(35, "hatchery-larva-1", hasHatcheryParent: true, hatcheryUnitTag: "hatchery"),
                Unit(35, "hatchery-larva-2", hasHatcheryParent: true, hatcheryUnitTag: "hatchery"),
                Unit(35, "lair-larva", hasHatcheryParent: true, hatcheryUnitTag: "lair"),
                Unit(35, "hive-larva-1", hasHatcheryParent: true, hatcheryUnitTag: "hive"),
                Unit(35, "hive-larva-2", hasHatcheryParent: true, hatcheryUnitTag: "hive"),
                Unit(35, "dead-hive-larva", hitPointsRaw: 0, hasHatcheryParent: true, hatcheryUnitTag: "hive"),
                Unit(35, "enemy-hatchery-larva", ownerId: 1, hasHatcheryParent: true, hatcheryUnitTag: "hatchery"),
                Unit(35, "hallucinated-lair-larva", isHallucination: true, hasHatcheryParent: true, hatcheryUnitTag: "lair"));

            var states = projected.SpatialStates.ToDictionary(item => item.UnitTag, StringComparer.Ordinal);
            Assert.Equal(131, states["hatchery"].UnitId);
            Assert.Equal(132, states["lair"].UnitId);
            Assert.Equal(133, states["hive"].UnitId);
            Assert.Equal((uint?)73, states["hatchery"].LarvaRemainingFrames);
            Assert.Equal((uint?)41, states["lair"].LarvaRemainingFrames);
            Assert.Equal((uint?)19, states["hive"].LarvaRemainingFrames);
            Assert.Equal((int?)2, states["hatchery"].ConnectedLarvaCount);
            Assert.Equal((int?)1, states["lair"].ConnectedLarvaCount);
            Assert.Equal((int?)2, states["hive"].ConnectedLarvaCount);
            Assert.Equal((uint?)73, projected.Runtime.Units.Single(item => item.UnitTag == "hatchery").LarvaRemainingFrames);
            Assert.Equal((int?)2, projected.Runtime.Units.Single(item => item.UnitTag == "hatchery").ConnectedLarvaCount);
        }

        [Fact]
        public void PreservesKnownZeroSeparatelyFromUnavailableLarvaFacts()
        {
            var knownZero = Project(
                Unit(131, "empty-hatchery", remainingTrainFrames: 0));
            var unknown = Project(
                Unit(131, "uncertain-hatchery", remainingTrainFrames: null),
                Unit(35, "uncertain-larva", hasHatcheryParent: null));

            var knownState = knownZero.SpatialStates.Single(item => item.UnitTag == "empty-hatchery");
            var unknownState = unknown.SpatialStates.Single(item => item.UnitTag == "uncertain-hatchery");
            Assert.Equal((uint?)0, knownState.LarvaRemainingFrames);
            Assert.Equal((int?)0, knownState.ConnectedLarvaCount);
            Assert.Null(unknownState.LarvaRemainingFrames);
            Assert.Null(unknownState.ConnectedLarvaCount);

            var frozenKnown = FrozenSemanticSnapshot.Freeze(SnapshotWith(knownState));
            var frozenUnknown = FrozenSemanticSnapshot.Freeze(SnapshotWith(unknownState));
            Assert.Equal((uint?)0, frozenKnown.UnitSpatialStates.Single().LarvaRemainingFrames);
            Assert.Equal((int?)0, frozenKnown.UnitSpatialStates.Single().ConnectedLarvaCount);
            Assert.Null(frozenUnknown.UnitSpatialStates.Single().LarvaRemainingFrames);
            Assert.Null(frozenUnknown.UnitSpatialStates.Single().ConnectedLarvaCount);
            Assert.NotEqual(
                Key(knownState.LarvaRemainingFrames, knownState.ConnectedLarvaCount),
                Key(unknownState.LarvaRemainingFrames, unknownState.ConnectedLarvaCount));
        }

        [Fact]
        public void FrozenSnapshotOwnsLarvaFactsAndSemanticKeyTracksTimerAndCountChanges()
        {
            var sourceState = new UnitSpatialState
            {
                UnitTag = "hatchery",
                UnitId = 131,
                LarvaRemainingFrames = 24,
                ConnectedLarvaCount = 2
            };
            var frozen = FrozenSemanticSnapshot.Freeze(SnapshotWith(sourceState));
            sourceState.LarvaRemainingFrames = 11;
            sourceState.ConnectedLarvaCount = 0;

            var frozenState = frozen.UnitSpatialStates.Single();
            Assert.Equal((uint?)24, frozenState.LarvaRemainingFrames);
            Assert.Equal((int?)2, frozenState.ConnectedLarvaCount);

            var baseline = Key(24, 2);
            Assert.Equal(baseline, Key(24, 2));
            Assert.NotEqual(baseline, Key(23, 2));
            Assert.NotEqual(baseline, Key(24, 1));
        }

        [Fact]
        public void IncompleteHatcheryMorphStillCarriesAValidLarvaTimer()
        {
            var projected = Project(
                Unit(132, "morphing-lair", isCompleted: false, remainingTrainFrames: 27));
            var state = projected.SpatialStates.Single(item => item.UnitTag == "morphing-lair");

            Assert.Equal((bool?)false, state.IsCompleted);
            Assert.Equal((uint?)27, state.LarvaRemainingFrames);
        }

        [Fact]
        public void LarvaTokenRoundsObservedFramesAndRespectsConnectionAndVisibility()
        {
            Assert.Equal(string.Empty, Token(131, 0, 0));
            Assert.Equal("larva=1", Token(131, 1, 0));
            Assert.Equal("larva=1", Token(131, 24, 0));
            Assert.Equal("larva=2", Token(131, 25, 0));
            Assert.Equal("larva=1", Token(131, 24, null));
            Assert.Equal(string.Empty, Token(131, null, 0));
            Assert.Equal(string.Empty, Token(131, 24, 3));
            Assert.Equal(string.Empty, Token(131, 24, 0, visible: false));
            Assert.Equal(string.Empty, SpatialPresenter.BuildLarvaToken(new UnitSpatialState
            {
                UnitId = 131,
                HitPointsRaw = 0,
                LarvaRemainingFrames = 24,
                ConnectedLarvaCount = 1
            }, Preferences()));
        }

        [Theory]
        [InlineData(131)]
        [InlineData(132)]
        [InlineData(133)]
        public void LarvaTokenRemainsAvailableDuringAnIncompleteMorph(int unitId)
        {
            var state = new UnitSpatialState
            {
                UnitId = unitId,
                HitPointsRaw = 25600,
                IsCompleted = false,
                LarvaRemainingFrames = 24,
                ConnectedLarvaCount = 1
            };

            Assert.Equal("larva=1", SpatialPresenter.BuildLarvaToken(state, Preferences()));
        }

        [Fact]
        public void LarvaVisibilityDefaultsRoundTripsAndControlsViewportDemand()
        {
            var layout = HudLayoutConfig.CreateDefault();
            layout.ShowTransportCargo = false;
            layout.ShowMorphTargetIcons = false;
            layout.ShowSpellTimers = false;
            layout.ShowBurrowedUnits = false;
            foreach (var key in layout.AbilityDisplayModes.Keys.ToArray())
            {
                layout.SetAbilityDisplayMode(key, MalcoPreferenceValues.AbilityHidden);
            }
            var controller = new SettingsController(layout);
            var initial = controller.Capture();

            Assert.True(initial.Snapshot.ShowLarvaCountdown);
            Assert.True(HudDisplayPreferences.FromLayout(initial.Snapshot.ToMutable()).HasUnitSpatialOverlays);

            var hidden = controller.ApplyEdit(SettingsEdit.SetLarvaCountdownVisible(false));
            Assert.True(hidden.Changed);
            Assert.False(controller.Capture().Snapshot.ShowLarvaCountdown);
            var hiddenLayout = controller.Capture().Snapshot.ToMutable();
            var hiddenPreferences = HudDisplayPreferences.FromLayout(hiddenLayout);
            Assert.False(hiddenPreferences.ShowLarvaCountdown);
            Assert.False(hiddenPreferences.HasUnitSpatialOverlays);

            var shown = controller.ApplyEdit(SettingsEdit.SetLarvaCountdownVisible(true));
            Assert.True(shown.Changed);
            var shownSnapshot = controller.Capture().Snapshot;
            Assert.True(shownSnapshot.ShowLarvaCountdown);
            Assert.True(HudDisplayPreferences.FromLayout(shownSnapshot.ToMutable()).HasUnitSpatialOverlays);
        }

        [Fact]
        public void Schema3WithoutLarvaFlagLoadsEnabledAndPersistsExplicitChoiceWithoutLosingOtherSettings()
        {
            var temporaryDirectory = Path.Combine(
                Path.GetTempPath(),
                "Malco-Larva-Persistence-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryDirectory);
            try
            {
                var path = Path.Combine(temporaryDirectory, "hud-layout.json");
                var oldSchema3Json = @"{
  ""SchemaVersion"": 3,
  ""Language"": ""ko_KR"",
  ""CompletionDisplayMode"": ""progress"",
  ""CompletionCountdownSeconds"": 15,
  ""UnitIconSize"": ""small"",
  ""BuildingIconSize"": ""large"",
  ""WorkerCountStyle"": ""white"",
  ""Widgets"": {
    ""units"": {
      ""Enabled"": false,
      ""X"": 123.0,
      ""Y"": 45.0,
      ""Width"": 640.0,
      ""Height"": 360.0,
      ""HasRelativeBounds"": true,
      ""XRatio"": 0.1,
      ""YRatio"": 0.2,
      ""WidthRatio"": 0.5,
      ""HeightRatio"": 0.5
    }
  },
  ""ItemSettings"": {
    ""unit:1"": { ""Show"": false, ""AvailableAlert"": false, ""CompletionAlert"": true }
  },
  ""AbilityDisplayModes"": { ""unit:71"": ""energy"" },
  ""ShowTransportCargo"": false
}";
                File.WriteAllText(path, oldSchema3Json);

                var legacyRead = new HudLayoutFileStore(path).Load();
                Assert.Equal(LayoutLoadStatus.Loaded, legacyRead.Status);
                Assert.True(legacyRead.Layout.ShowLarvaCountdown);
                Assert.False(legacyRead.Layout.ShowTransportCargo);
                Assert.Equal("ko_KR", legacyRead.Layout.Language);
                Assert.False(legacyRead.Layout.Widgets["units"].Enabled);
                Assert.Equal(123d, legacyRead.Layout.Widgets["units"].X);
                Assert.Equal(640d, legacyRead.Layout.Widgets["units"].Width);
                Assert.False(legacyRead.Layout.ItemSettings["unit:1"].Show);
                Assert.False(legacyRead.Layout.ItemSettings["unit:1"].AvailableAlert);
                Assert.True(legacyRead.Layout.ItemSettings["unit:1"].CompletionAlert);
                Assert.Equal("energy", legacyRead.Layout.AbilityDisplayModes["unit:71"]);

                legacyRead.Layout.ShowLarvaCountdown = false;
                var snapshot = HudLayoutSnapshot.FromLayout(legacyRead.Layout);
                var save = new HudLayoutFileStore(path).Save(snapshot);
                Assert.Equal(LayoutSaveStatus.Saved, save.Status);

                var reloaded = new HudLayoutFileStore(path).Load();
                Assert.Equal(LayoutLoadStatus.Loaded, reloaded.Status);
                Assert.False(reloaded.Layout.ShowLarvaCountdown);
                Assert.False(reloaded.Layout.ShowTransportCargo);
                Assert.Equal("ko_KR", reloaded.Layout.Language);
                Assert.False(reloaded.Layout.Widgets["units"].Enabled);
                Assert.Equal(123d, reloaded.Layout.Widgets["units"].X);
                Assert.Equal(640d, reloaded.Layout.Widgets["units"].Width);
                Assert.False(reloaded.Layout.ItemSettings["unit:1"].Show);
                Assert.False(reloaded.Layout.ItemSettings["unit:1"].AvailableAlert);
                Assert.True(reloaded.Layout.ItemSettings["unit:1"].CompletionAlert);
                Assert.Equal("energy", reloaded.Layout.AbilityDisplayModes["unit:71"]);
            }
            finally
            {
                if (Directory.Exists(temporaryDirectory))
                {
                    Directory.Delete(temporaryDirectory, recursive: true);
                }
            }
        }

        private static string Key(uint? remainingFrames, int? connectedLarvaCount)
        {
            return GameSnapshotSemanticKey.Build(SnapshotWith(new UnitSpatialState
            {
                UnitTag = "hatchery",
                UnitId = 131,
                LarvaRemainingFrames = remainingFrames,
                ConnectedLarvaCount = connectedLarvaCount
            }));
        }

        private static string Token(int unitId, uint? remainingFrames, int? connectedLarvaCount, bool visible = true)
        {
            return SpatialPresenter.BuildLarvaToken(new UnitSpatialState
            {
                UnitId = unitId,
                HitPointsRaw = 25600,
                LarvaRemainingFrames = remainingFrames,
                ConnectedLarvaCount = connectedLarvaCount
            }, Preferences(visible));
        }

        private static HudDisplayPreferences Preferences(bool visible = true)
        {
            var layout = HudLayoutConfig.CreateDefault();
            layout.ShowLarvaCountdown = visible;
            layout.ShowTransportCargo = false;
            return HudDisplayPreferences.FromLayout(layout);
        }

        private static ProjectionResult Project(params BwrApiSemanticUnitV1[] units)
        {
            var source = (BwrApiSemanticSnapshotV1)typeof(BwrApiSemanticSnapshotV1)
                .GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)
                .Single()
                .Invoke(new object[]
                {
                    "test",
                    "larva-countdown",
                    1UL,
                    10U,
                    BwrApiSemanticSnapshotStatus.Complete,
                    new BwrApiSemanticSessionV1(true, null, null, false),
                    (byte?)0,
                    null,
                    new[] { new BwrApiSemanticPlayerV1(0, 1, null, null, null, null, null, null) },
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
            var runtime = new BwrApiSemanticRuntimeMapper().Map(
                new BwrApiFrameHeader(
                    1,
                    BwrApiDataState.Complete,
                    BwrApiErrorKind.None,
                    "larva-countdown",
                    1,
                    1,
                    10),
                source);
            var states = new UnitSpatialStateProjector()
                .Build(runtime.Units, runtime.CompletedUnitCounts);
            return new ProjectionResult(runtime, states);
        }

        private static BwrApiSemanticUnitV1 Unit(
            int unitId,
            string unitTag,
            byte ownerId = 0,
            int hitPointsRaw = 25600,
            bool? isCompleted = true,
            bool isHallucination = false,
            uint? remainingTrainFrames = null,
            bool? hasHatcheryParent = null,
            string hatcheryUnitTag = null)
        {
            var constructor = typeof(BwrApiSemanticUnitV1)
                .GetConstructors()
                .OrderByDescending(item => item.GetParameters().Length)
                .First();
            var values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["unitTag"] = unitTag,
                ["ownerId"] = ownerId,
                ["unitTypeId"] = (ushort)unitId,
                ["hitPointsRaw"] = hitPointsRaw,
                ["isCompleted"] = isCompleted,
                ["isHallucination"] = isHallucination
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
                RemainingTrainFrames = remainingTrainFrames,
                HasHatcheryParent = hasHatcheryParent,
                HatcheryUnitTag = hatcheryUnitTag
            };
        }

        private static GameSnapshot SnapshotWith(UnitSpatialState unit)
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
                new[] { unit });
        }

        private sealed class ProjectionResult
        {
            public ProjectionResult(BwrApiRuntimeSnapshot runtime, IReadOnlyList<UnitSpatialState> spatialStates)
            {
                Runtime = runtime;
                SpatialStates = spatialStates;
            }

            public BwrApiRuntimeSnapshot Runtime { get; }
            public IReadOnlyList<UnitSpatialState> SpatialStates { get; }
        }
    }
}
