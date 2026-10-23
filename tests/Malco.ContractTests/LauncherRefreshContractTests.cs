using System;
using System.IO;
using Malco.Updates;
using Xunit;

namespace Malco.ContractTests
{
    public sealed class LauncherRefreshContractTests : IDisposable
    {
        private const int ExitedProcessId = int.MaxValue;
        private readonly string _root = Path.Combine(Path.GetTempPath(), "malco-launcher-refresh-" + Guid.NewGuid().ToString("N"));
        private readonly string _bundled;

        public LauncherRefreshContractTests()
        {
            _bundled = Path.Combine(_root, "versions", "launcher");
            Directory.CreateDirectory(_bundled);
            File.WriteAllText(Path.Combine(_root, "Malco.Launcher.exe"), "old launcher");
            File.WriteAllText(Path.Combine(_root, "launcher-policy.json"), "{\"maximum_payload_bytes\":13000000}");
            File.WriteAllText(Path.Combine(_bundled, "Malco.Launcher.exe"), "new launcher");
            File.WriteAllText(Path.Combine(_bundled, "launcher-policy.json"), "{\"maximum_payload_bytes\":33554432}");
        }

        public void Dispose() => Directory.Delete(_root, true);

        [Fact]
        public void OlderInstallRootGetsTheBundledLauncherPair()
        {
            Assert.True(LauncherRefreshNotice.IsLauncherOutdated(_root, Path.Combine(_bundled, "launcher-policy.json")));
            Assert.True(LauncherRefreshNotice.TryReplaceLauncher(_root, _bundled, ExitedProcessId, 0));

            Assert.Equal("new launcher", File.ReadAllText(Path.Combine(_root, "Malco.Launcher.exe")));
            Assert.False(LauncherRefreshNotice.IsLauncherOutdated(_root, Path.Combine(_bundled, "launcher-policy.json")));
            Assert.Equal(2, Directory.GetFiles(_root).Length);
        }

        [Fact]
        public void FailedPolicyReplacementRestoresTheOldLauncher()
        {
            using (new FileStream(Path.Combine(_root, "launcher-policy.json"), FileMode.Open, FileAccess.Read, FileShare.None))
            {
                Assert.False(LauncherRefreshNotice.TryReplaceLauncher(_root, _bundled, ExitedProcessId, 0));
            }

            Assert.Equal("old launcher", File.ReadAllText(Path.Combine(_root, "Malco.Launcher.exe")));
            Assert.Equal("{\"maximum_payload_bytes\":13000000}", File.ReadAllText(Path.Combine(_root, "launcher-policy.json")));
        }
    }
}
