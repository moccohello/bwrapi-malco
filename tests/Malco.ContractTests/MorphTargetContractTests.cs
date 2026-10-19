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
    public sealed class MorphTargetContractTests
    {
        [Fact]
        public void PinnedSdkProvidesNullableBuildTypeId()
        {
            Assert.Equal(
                typeof(ushort?),
                typeof(BwrApiSemanticUnitV1).GetProperty("BuildTypeId").PropertyType);
        }

        [Theory]
        [InlineData(36, 37)]
        [InlineData(36, 38)]
        [InlineData(36, 39)]
        [InlineData(36, 41)]
        [InlineData(36, 42)]
        [InlineData(36, 43)]
        [InlineData(36, 45)]
        [InlineData(36, 46)]
        [InlineData(36, 47)]
        [InlineData(59, 44)]
        [InlineData(59, 62)]
        public void MapsSupportedSdkBuildTypesThroughFrozenStateAndSemanticKey(int sourceTypeId, int targetTypeId)
        {
            var mapped = MapAndProject(sourceTypeId, (ushort?)targetTypeId);
            Assert.Equal((int?)targetTypeId, mapped.Runtime.MorphTargetUnitId);
            Assert.Equal((int?)targetTypeId, mapped.State.MorphTargetUnitId);
            Assert.Equal("morph=" + targetTypeId, SpatialPresenter.BuildMorphTargetToken(mapped.State, Preferences()));

            var frozen = FrozenSemanticSnapshot.Freeze(SnapshotWith(mapped.State));
            Assert.Equal((int?)targetTypeId, frozen.UnitSpatialStates.Single().MorphTargetUnitId);

            mapped.State.MorphTargetUnitId = 0;
            Assert.Equal((int?)targetTypeId, frozen.UnitSpatialStates.Single().MorphTargetUnitId);

            var nextTarget = MapAndProject(sourceTypeId, (ushort?)(targetTypeId + 1));
            Assert.NotEqual(
                GameSnapshotSemanticKey.Build(SnapshotWith(frozen.UnitSpatialStates.Single())),
                GameSnapshotSemanticKey.Build(SnapshotWith(nextTarget.State)));
        }

        [Theory]
        [InlineData(36, 0)]
        [InlineData(36, 228)]
        [InlineData(59, 0)]
        [InlineData(59, 42)]
        [InlineData(59, 228)]
        public void PreservesRawUnknownOrInvalidBuildTypeButSuppressesItsHudToken(int sourceTypeId, int rawTargetId)
        {
            var mapped = MapAndProject(sourceTypeId, (ushort?)rawTargetId);

            Assert.Equal((int?)rawTargetId, mapped.Runtime.MorphTargetUnitId);
            Assert.Equal((int?)rawTargetId, mapped.State.MorphTargetUnitId);
            Assert.Equal(string.Empty, SpatialPresenter.BuildMorphTargetToken(mapped.State, Preferences()));

            var frozen = FrozenSemanticSnapshot.Freeze(SnapshotWith(mapped.State));
            Assert.Equal((int?)rawTargetId, frozen.UnitSpatialStates.Single().MorphTargetUnitId);
        }

        [Theory]
        [InlineData(36)]
        [InlineData(59)]
        public void MissingBuildTypeRemainsUnknownAndSuppressesItsHudToken(int sourceTypeId)
        {
            var mapped = MapAndProject(sourceTypeId, null);

            Assert.Null(mapped.Runtime.MorphTargetUnitId);
            Assert.Null(mapped.State.MorphTargetUnitId);
            Assert.Equal(string.Empty, SpatialPresenter.BuildMorphTargetToken(mapped.State, Preferences()));

            var frozen = FrozenSemanticSnapshot.Freeze(SnapshotWith(mapped.State));
            Assert.Null(frozen.UnitSpatialStates.Single().MorphTargetUnitId);
        }

        [Fact]
        public void MorphIconPreferenceDefaultsRoundTripsAndControlsViewportDemand()
        {
            var layout = HudLayoutConfig.CreateDefault();
            DisableOtherSpatialOverlays(layout);
            var controller = new SettingsController(layout);
            var initial = controller.Capture();

            Assert.True(initial.Snapshot.ShowMorphTargetIcons);
            Assert.True(HudDisplayPreferences.FromLayout(initial.Snapshot.ToMutable()).HasUnitSpatialOverlays);

            var hidden = controller.ApplyEdit(SettingsEdit.SetMorphTargetIconsVisible(false));
            Assert.True(hidden.Changed);
            var hiddenSnapshot = controller.Capture().Snapshot;
            Assert.False(hiddenSnapshot.ShowMorphTargetIcons);
            var hiddenPreferences = HudDisplayPreferences.FromLayout(hiddenSnapshot.ToMutable());
            Assert.False(hiddenPreferences.ShowMorphTargetIcons);
            Assert.False(hiddenPreferences.HasUnitSpatialOverlays);

            var shown = controller.ApplyEdit(SettingsEdit.SetMorphTargetIconsVisible(true));
            Assert.True(shown.Changed);
            var shownSnapshot = controller.Capture().Snapshot;
            Assert.True(shownSnapshot.ShowMorphTargetIcons);
            Assert.True(HudDisplayPreferences.FromLayout(shownSnapshot.ToMutable()).HasUnitSpatialOverlays);
        }

        [Fact]
        public void Schema3WithoutMorphIconFlagLoadsEnabledAndPersistsTheExplicitChoice()
        {
            var temporaryDirectory = Path.Combine(
                Path.GetTempPath(),
                "Malco-Morph-Persistence-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryDirectory);
            try
            {
                var path = Path.Combine(temporaryDirectory, "hud-layout.json");
                var oldSchema3Json = @"{
  ""SchemaVersion"": 3,
  ""Language"": ""en_US"",
  ""ShowLarvaCountdown"": false,
  ""ShowTransportCargo"": false,
  ""Widgets"": {
    ""units"": {
      ""Enabled"": false,
      ""X"": 77.0,
      ""Y"": 38.0,
      ""Width"": 640.0,
      ""Height"": 360.0
    }
  }
}";
                File.WriteAllText(path, oldSchema3Json);

                var loaded = new HudLayoutFileStore(path).Load();
                Assert.Equal(LayoutLoadStatus.Loaded, loaded.Status);
                Assert.True(loaded.Layout.ShowMorphTargetIcons);
                Assert.False(loaded.Layout.ShowLarvaCountdown);
                Assert.False(loaded.Layout.ShowTransportCargo);
                Assert.Equal("en_US", loaded.Layout.Language);
                Assert.False(loaded.Layout.Widgets["units"].Enabled);
                Assert.Equal(77d, loaded.Layout.Widgets["units"].X);
                Assert.Equal(640d, loaded.Layout.Widgets["units"].Width);

                var snapshot = HudLayoutSnapshot.FromLayout(loaded.Layout);
                Assert.True(snapshot.ShowMorphTargetIcons);
                var mutable = snapshot.ToMutable();
                mutable.ShowMorphTargetIcons = false;
                var save = new HudLayoutFileStore(path).Save(HudLayoutSnapshot.FromLayout(mutable));
                Assert.Equal(LayoutSaveStatus.Saved, save.Status);

                var reloaded = new HudLayoutFileStore(path).Load();
                Assert.Equal(LayoutLoadStatus.Loaded, reloaded.Status);
                Assert.False(reloaded.Layout.ShowMorphTargetIcons);
                Assert.False(reloaded.Layout.ShowLarvaCountdown);
                Assert.False(reloaded.Layout.ShowTransportCargo);
                Assert.Equal("en_US", reloaded.Layout.Language);
                Assert.False(reloaded.Layout.Widgets["units"].Enabled);
                Assert.Equal(77d, reloaded.Layout.Widgets["units"].X);
                Assert.Equal(640d, reloaded.Layout.Widgets["units"].Width);
            }
            finally
            {
                if (Directory.Exists(temporaryDirectory))
                {
                    Directory.Delete(temporaryDirectory, recursive: true);
                }
            }
        }

        private static MappedUnit MapAndProject(int sourceTypeId, ushort? buildTypeId)
        {
            var unit = Unit(sourceTypeId, buildTypeId);
            var source = (BwrApiSemanticSnapshotV1)typeof(BwrApiSemanticSnapshotV1)
                .GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)
                .Single()
                .Invoke(new object[]
                {
                    "test",
                    "morph-target",
                    1UL,
                    10U,
                    BwrApiSemanticSnapshotStatus.Complete,
                    new BwrApiSemanticSessionV1(true, null, null, false),
                    (byte?)0,
                    null,
                    new[] { new BwrApiSemanticPlayerV1(0, 1, null, null, null, null, null, null) },
                    Array.Empty<BwrApiSemanticUnitCountV1>(),
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
            var runtime = new BwrApiSemanticRuntimeMapper().Map(
                new BwrApiFrameHeader(
                    1,
                    BwrApiDataState.Complete,
                    BwrApiErrorKind.None,
                    "morph-target",
                    1,
                    1,
                    10),
                source);
            var runtimeUnit = runtime.Units.Single();
            var state = new UnitSpatialStateProjector()
                .Build(runtime.Units, runtime.CompletedUnitCounts)
                .Single();
            return new MappedUnit(runtimeUnit, state);
        }

        private static BwrApiSemanticUnitV1 Unit(int unitTypeId, ushort? buildTypeId)
        {
            var constructor = typeof(BwrApiSemanticUnitV1)
                .GetConstructors()
                .OrderByDescending(item => item.GetParameters().Length)
                .First();
            var values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["unitTag"] = "morph-" + unitTypeId,
                ["ownerId"] = (byte)0,
                ["unitTypeId"] = (ushort)unitTypeId,
                ["buildTypeId"] = buildTypeId,
                ["hitPointsRaw"] = 25600,
                ["isCompleted"] = false,
                ["isHallucination"] = false
            };
            var unit = (BwrApiSemanticUnitV1)constructor.Invoke(constructor.GetParameters()
                .Select(parameter => values.TryGetValue(parameter.Name, out var value)
                    ? value
                    : parameter.ParameterType.IsValueType
                        ? Activator.CreateInstance(parameter.ParameterType)
                        : null)
                .ToArray());
            return unit with { BuildTypeId = buildTypeId };
        }

        private static HudDisplayPreferences Preferences()
        {
            var layout = HudLayoutConfig.CreateDefault();
            DisableOtherSpatialOverlays(layout);
            return HudDisplayPreferences.FromLayout(layout);
        }

        private static void DisableOtherSpatialOverlays(HudLayoutConfig layout)
        {
            layout.ShowTransportCargo = false;
            layout.ShowLarvaCountdown = false;
            foreach (var key in layout.AbilityDisplayModes.Keys.ToArray())
            {
                layout.SetAbilityDisplayMode(key, MalcoPreferenceValues.AbilityHidden);
            }
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

        private sealed class MappedUnit
        {
            public MappedUnit(BwrApiRuntimeUnit runtime, UnitSpatialState state)
            {
                Runtime = runtime;
                State = state;
            }

            public BwrApiRuntimeUnit Runtime { get; }
            public UnitSpatialState State { get; }
        }
    }
}
