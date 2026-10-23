using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Malco.Localization;

namespace Malco.Updates
{
    // The launcher and its policy live in the install root and only Setup
    // installs them. A launcher whose limits are below the policy shipped with
    // this release would silently skip later releases, so the release carries
    // its launcher and replaces the old pair once that launcher has exited.
    // The reinstall notice is only the fallback when the replacement fails.
    internal static class LauncherRefreshNotice
    {
        private const string DownloadUrl = "https://horangmalco.com/downloads/malco/latest";
        private const string LauncherName = "Malco.Launcher.exe";
        private const string PolicyName = "launcher-policy.json";
        private static readonly TimeSpan LauncherExitWait = TimeSpan.FromMinutes(2);

        public static void RefreshIfLauncherOutdated(
            Func<string> installDirectory,
            int launcherProcessId,
            long launcherStartTimeUtcTicks,
            Dispatcher dispatcher)
        {
            Task.Run(() =>
            {
                string root;
                try
                {
                    root = installDirectory();
                }
                catch (Exception ex) when (ex is InvalidOperationException || ex is IOException)
                {
                    return;
                }
                var bundled = Path.Combine(AppContext.BaseDirectory, "launcher");
                if (!IsLauncherOutdated(root, Path.Combine(bundled, PolicyName))) return;
                if (TryReplaceLauncher(root, bundled, launcherProcessId, launcherStartTimeUtcTicks)) return;
                dispatcher.BeginInvoke(new Action(ShowReinstallNotice));
            });
        }

        internal static bool IsLauncherOutdated(string installDirectory, string bundledPolicyPath)
        {
            try
            {
                using (var builtPolicy = JsonDocument.Parse(File.ReadAllBytes(bundledPolicyPath)))
                using (var installedPolicy = JsonDocument.Parse(
                           File.ReadAllBytes(Path.Combine(installDirectory, PolicyName))))
                {
                    return IsBelow(installedPolicy.RootElement, builtPolicy.RootElement);
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException ||
                                       ex is JsonException || ex is InvalidOperationException ||
                                       ex is FormatException)
            {
                return false;
            }
        }

        // Every "maximum_*" value is a size or count limit the launcher enforces.
        internal static bool IsBelow(JsonElement installed, JsonElement built)
        {
            foreach (var builtLimit in built.EnumerateObject())
            {
                if (builtLimit.Name.StartsWith("maximum_", StringComparison.Ordinal) &&
                    installed.TryGetProperty(builtLimit.Name, out var installedLimit) &&
                    installedLimit.GetInt64() < builtLimit.Value.GetInt64())
                    return true;
            }
            return false;
        }

        // The launcher only reads a policy whose keys match its own build, so
        // the executable and policy are replaced as a pair and restored together
        // if the second replacement fails.
        internal static bool TryReplaceLauncher(
            string root,
            string bundledDirectory,
            int launcherProcessId,
            long launcherStartTimeUtcTicks)
        {
            var launcher = Path.Combine(root, LauncherName);
            var policy = Path.Combine(root, PolicyName);
            try
            {
                if (!WaitForLauncherExit(launcherProcessId, launcherStartTimeUtcTicks)) return false;
                File.Copy(Path.Combine(bundledDirectory, LauncherName), launcher + ".update", true);
                File.Copy(Path.Combine(bundledDirectory, PolicyName), policy + ".update", true);
                File.Replace(launcher + ".update", launcher, launcher + ".previous");
                try
                {
                    File.Replace(policy + ".update", policy, policy + ".previous");
                }
                catch
                {
                    File.Replace(launcher + ".previous", launcher, null);
                    throw;
                }
                File.Delete(launcher + ".previous");
                File.Delete(policy + ".previous");
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                TryDelete(launcher + ".update");
                TryDelete(policy + ".update");
                return false;
            }
        }

        private static bool WaitForLauncherExit(int processId, long startTimeUtcTicks)
        {
            Process process;
            try
            {
                process = Process.GetProcessById(processId);
            }
            catch (ArgumentException)
            {
                return true;
            }
            using (process)
            {
                try
                {
                    if (process.StartTime.ToUniversalTime().Ticks != startTimeUtcTicks) return true;
                    return process.WaitForExit((int)LauncherExitWait.TotalMilliseconds);
                }
                catch (InvalidOperationException)
                {
                    return true;
                }
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
            }
        }

        private static void ShowReinstallNotice()
        {
            var answer = MessageBox.Show(
                UiText.Get("A new Malco installer is required to keep receiving updates. Open the download page?"),
                "Malco",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information,
                MessageBoxResult.Yes);
            if (answer != MessageBoxResult.Yes) return;
            try
            {
                Process.Start(new ProcessStartInfo(DownloadUrl) { UseShellExecute = true })?.Dispose();
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is System.ComponentModel.Win32Exception)
            {
            }
        }
    }
}
