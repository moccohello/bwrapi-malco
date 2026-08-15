using Malco.Settings.Contracts;
using Xunit;

namespace Malco.ContractTests
{
    public sealed class SettingsContractTests
    {
        [Fact]
        public void GroupEditOwnsItsKeysAndRejectsBlankTargets()
        {
            var source = new[] { "workers", "units" };
            var edit = SettingsEdit.SetItemsShown(source, true);

            source[0] = "changed";
            var returned = edit.Keys;
            returned[1] = "changed";

            Assert.True(edit.HasTarget);
            Assert.Equal(new[] { "workers", "units" }, edit.Keys);
            Assert.False(SettingsEdit.SetItemsShown(new[] { "workers", " " }, true).HasTarget);
        }

        [Fact]
        public void SingleEditRequiresARealTarget()
        {
            Assert.True(SettingsEdit.SetWidgetEnabled("workers", true).HasTarget);
            Assert.False(SettingsEdit.SetWidgetEnabled(" ", true).HasTarget);
        }

        [Fact]
        public void FailedEditorExitIsVetoed()
        {
            var result = new SettingsFlushResult(
                SettingsFlushStatus.Failed,
                SettingsFlushReason.EditorExit,
                3,
                4,
                "save failed");

            Assert.False(result.Succeeded);
            Assert.True(result.ShouldVetoEditorExit);
            Assert.False(result.ShouldContinueShutdown);
        }

        [Theory]
        [InlineData((int)SettingsFlushStatus.NoChanges)]
        [InlineData((int)SettingsFlushStatus.Saved)]
        public void SuccessfulShutdownMayContinue(int statusValue)
        {
            var status = (SettingsFlushStatus)statusValue;
            var result = new SettingsFlushResult(status, SettingsFlushReason.Shutdown, 5, string.Empty);

            Assert.True(result.Succeeded);
            Assert.True(result.ShouldContinueShutdown);
            Assert.False(result.ShouldVetoEditorExit);
        }
    }
}
