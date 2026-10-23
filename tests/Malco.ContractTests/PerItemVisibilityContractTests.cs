using System;
using System.IO;
using Malco;
using Malco.Configuration;
using Malco.Configuration.Models;
using Malco.Settings.Contracts;
using Malco.Settings.Controller;
using Xunit;

namespace Malco.ContractTests
{
    public sealed class PerItemVisibilityContractTests
    {
        [Fact]
        public void LayoutWithoutHiddenListsShowsEverything()
        {
            WithLayoutFile(@"{ ""SchemaVersion"": 3, ""ShowSpellTimers"": true, ""ShowBurrowedUnits"": true }", path =>
            {
                var loaded = new HudLayoutFileStore(path).Load();
                Assert.Equal(LayoutLoadStatus.Loaded, loaded.Status);
                var preferences = HudDisplayPreferences.FromLayout(loaded.Layout);
                foreach (var kind in new[] { "Lockdown", "Psionic Storm", "Plague" })
                {
                    Assert.True(preferences.IsSpellTimerShown(kind));
                }
                foreach (var unitId in new[] { 41, 37, 103 })
                {
                    Assert.True(preferences.IsBurrowedUnitShown(unitId));
                }
            });
        }

        [Fact]
        public void HiddenSpellsAndBurrowedUnitsRoundTripThroughSaveAndLoad()
        {
            var controller = new SettingsController(HudLayoutConfig.CreateDefault());
            Assert.True(controller.ApplyEdit(SettingsEdit.SetSpellTimerVisible("Psionic Storm", false)).Changed);
            Assert.False(controller.ApplyEdit(SettingsEdit.SetSpellTimerVisible("Psionic Storm", false)).Changed);
            Assert.False(controller.ApplyEdit(SettingsEdit.SetSpellTimerVisible("Unknown Spell", false)).Changed);
            Assert.True(controller.ApplyEdit(SettingsEdit.SetBurrowedUnitVisible(37, false)).Changed);
            Assert.False(controller.ApplyEdit(SettingsEdit.SetBurrowedUnitVisible(0, false)).Changed);

            WithLayoutFile(null, path =>
            {
                var store = new HudLayoutFileStore(path);
                Assert.Equal(LayoutSaveStatus.Saved, store.Save(controller.Capture().Snapshot).Status);
                var preferences = HudDisplayPreferences.FromLayout(new HudLayoutFileStore(path).Load().Layout);
                Assert.False(preferences.IsSpellTimerShown("Psionic Storm"));
                Assert.True(preferences.IsSpellTimerShown("Stasis Field"));
                Assert.False(preferences.IsBurrowedUnitShown(37));
                Assert.True(preferences.IsBurrowedUnitShown(41));
            });

            Assert.True(controller.ApplyEdit(SettingsEdit.SetSpellTimerVisible("Psionic Storm", true)).Changed);
            Assert.True(controller.ApplyEdit(SettingsEdit.SetBurrowedUnitsVisible(false)).Changed);
            var latest = HudDisplayPreferences.FromLayout(controller.Capture().Snapshot.ToMutable());
            Assert.True(latest.IsSpellTimerShown("Psionic Storm"));
            Assert.False(latest.IsBurrowedUnitShown(41));
        }

        private static void WithLayoutFile(string json, Action<string> body)
        {
            var directory = Path.Combine(Path.GetTempPath(), "Malco-ItemVisibility-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var path = Path.Combine(directory, "hud-layout.json");
                if (json != null)
                {
                    File.WriteAllText(path, json);
                }
                body(path);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
