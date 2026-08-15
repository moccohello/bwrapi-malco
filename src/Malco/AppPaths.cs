using System;
using System.IO;

namespace Malco
{
    internal static class AppPaths
    {
        private const string VersionsDirectoryName = "versions";
        private const string DataDirectoryName = "data";

        public static string InstallDirectory
        {
            get
            {
                var payloadDirectory = new DirectoryInfo(
                    Path.GetFullPath(AppContext.BaseDirectory));
                var versionDirectory = payloadDirectory.Parent;
                var versionsDirectory = versionDirectory?.Parent;
                var installDirectory = versionsDirectory?.Parent;
                if (string.Equals(
                        payloadDirectory.Name,
                        "payload",
                        StringComparison.OrdinalIgnoreCase) &&
                    versionDirectory != null &&
                    versionsDirectory != null &&
                    installDirectory != null &&
                    string.Equals(
                        versionsDirectory.Name,
                        VersionsDirectoryName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    EnsureOrdinaryDirectory(payloadDirectory, "payload");
                    EnsureOrdinaryDirectory(versionDirectory, "release");
                    EnsureOrdinaryDirectory(versionsDirectory, "versions");
                    EnsureOrdinaryDirectory(installDirectory, "install");
                    var markerPath = Path.Combine(installDirectory.FullName, ".malco-install-root");
                    var launcherPath = Path.Combine(installDirectory.FullName, "Malco.Launcher.exe");
                    if (!HasExactInstallMarker(markerPath) ||
                        !File.Exists(launcherPath) ||
                        (File.GetAttributes(launcherPath) & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
                    {
                        throw new InvalidOperationException(
                            "The versioned Malco process is not beneath a valid installed product root.");
                    }
                    return installDirectory.FullName;
                }

                return payloadDirectory.FullName;
            }
        }

        public static string UserDataDirectory => Path.Combine(InstallDirectory, DataDirectoryName);

        public static string UserLayoutPath => Path.Combine(UserDataDirectory, "hud-layout.json");

        private static void EnsureOrdinaryDirectory(DirectoryInfo directory, string label)
        {
            directory.Refresh();
            if (!directory.Exists ||
                (directory.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException("The Malco " + label + " directory is not an ordinary directory.");
            }
        }

        private static bool HasExactInstallMarker(string path)
        {
            var expected = System.Text.Encoding.ASCII.GetBytes("malco-install-root=1\r\n");
            try
            {
                if ((File.GetAttributes(path) & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
                {
                    return false;
                }
                using (var stream = new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    expected.Length,
                    FileOptions.SequentialScan))
                {
                    if (stream.Length != expected.Length) return false;
                    var actual = new byte[expected.Length];
                    var offset = 0;
                    while (offset < actual.Length)
                    {
                        var read = stream.Read(actual, offset, actual.Length - offset);
                        if (read == 0) return false;
                        offset += read;
                    }
                    return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(actual, expected);
                }
            }
            catch (Exception exception) when (
                exception is IOException || exception is UnauthorizedAccessException)
            {
                return false;
            }
        }
    }
}
