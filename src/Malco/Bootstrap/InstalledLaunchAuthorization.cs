using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Malco.Bootstrap
{
    internal sealed class InstalledLaunchAuthorization
    {
        private static readonly Regex TokenPattern = new Regex(
            "^[0-9a-f]{64}$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private readonly string _authorizationPath;
        private readonly string _versionDirectoryName;
        private readonly string _expectedLauncherPath;

        private InstalledLaunchAuthorization(
            string manifestSha256,
            string authorizationPath,
            string versionDirectoryName,
            string expectedLauncherPath,
            bool requiredUpdateRecheck,
            int launcherProcessId,
            long launcherStartTimeUtcTicks)
        {
            ManifestSha256 = manifestSha256;
            _authorizationPath = authorizationPath;
            _versionDirectoryName = versionDirectoryName;
            _expectedLauncherPath = expectedLauncherPath;
            RequiredUpdateRecheck = requiredUpdateRecheck;
            LauncherProcessId = launcherProcessId;
            LauncherStartTimeUtcTicks = launcherStartTimeUtcTicks;
        }

        public string ManifestSha256 { get; }

        public string LauncherPath => _expectedLauncherPath;

        public bool RequiredUpdateRecheck { get; }

        public int LauncherProcessId { get; }

        public long LauncherStartTimeUtcTicks { get; }

        public string ReleaseMutexName =>
            @"Local\Malco.Desktop.SingleInstance.v1.Release." + ManifestSha256;

        public static bool IsInstalledVersionLayout()
        {
            try
            {
                var payloadDirectory = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar));
                var versionDirectory = payloadDirectory.Parent;
                var versionsDirectory = versionDirectory?.Parent;
                return versionDirectory != null && versionsDirectory != null &&
                    string.Equals(payloadDirectory.Name, "payload", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(versionsDirectory.Name, "versions", StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is NotSupportedException ||
                exception is ArgumentException)
            {
                // An ambiguous installed-looking path is not a contributor
                // layout and must never gain a direct-start fallback.
                return true;
            }
        }

        public static bool TryConsume(
            string launchToken,
            out InstalledLaunchAuthorization authorization)
        {
            authorization = null;
            if (string.IsNullOrEmpty(launchToken) || !TokenPattern.IsMatch(launchToken))
            {
                return false;
            }
            InstalledLaunchAuthorization candidate;
            if (!TryResolve(launchToken, out candidate)) return false;

            byte[] actual;
            try
            {
                var info = new FileInfo(candidate._authorizationPath);
                if (!info.Exists ||
                    (info.Attributes & FileAttributes.ReparsePoint) != 0 ||
                    info.Length <= 0 || info.Length > 256)
                {
                    return false;
                }
                using (var stream = new FileStream(
                    candidate._authorizationPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.None,
                    4096,
                    FileOptions.DeleteOnClose))
                {
                    if (stream.Length <= 0 || stream.Length > 256) return false;
                    actual = new byte[checked((int)stream.Length)];
                    var offset = 0;
                    while (offset < actual.Length)
                    {
                        var read = stream.Read(actual, offset, actual.Length - offset);
                        if (read == 0) return false;
                        offset += read;
                    }
                }
                var text = new UTF8Encoding(false, true).GetString(actual);
                var fields = text.Split(new[] { "\r\n" }, StringSplitOptions.None);
                if (fields.Length != 6 ||
                    !string.Equals(fields[0], "malco.launch-authorization.v2", StringComparison.Ordinal) ||
                    !string.Equals(fields[1], candidate._versionDirectoryName, StringComparison.Ordinal) ||
                    (fields[4] != "0" && fields[4] != "1") ||
                    fields[5].Length != 0 ||
                    !int.TryParse(fields[2], NumberStyles.None, CultureInfo.InvariantCulture, out var launcherPid) ||
                    launcherPid <= 0 ||
                    !long.TryParse(fields[3], NumberStyles.None, CultureInfo.InvariantCulture, out var launcherStartTicks) ||
                    launcherStartTicks <= 0)
                {
                    return false;
                }
                if (File.Exists(candidate._authorizationPath)) return false;
                using (var launcher = Process.GetProcessById(launcherPid))
                {
                    if (launcher.HasExited ||
                        launcher.StartTime.ToUniversalTime().Ticks != launcherStartTicks ||
                        launcher.MainModule == null ||
                        !string.Equals(
                            Path.GetFullPath(launcher.MainModule.FileName),
                            candidate._expectedLauncherPath,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }
                authorization = new InstalledLaunchAuthorization(
                    candidate.ManifestSha256,
                    candidate._authorizationPath,
                    candidate._versionDirectoryName,
                    candidate._expectedLauncherPath,
                    fields[4] == "1",
                    launcherPid,
                    launcherStartTicks);
                return true;
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is NotSupportedException ||
                exception is ArgumentException ||
                exception is InvalidOperationException ||
                exception is System.ComponentModel.Win32Exception)
            {
                return false;
            }
        }

        private static bool TryResolve(
            string launchToken,
            out InstalledLaunchAuthorization layout)
        {
            layout = null;
            try
            {
                var payloadDirectory = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar));
                var versionDirectory = payloadDirectory.Parent;
                var versionsDirectory = versionDirectory?.Parent;
                var installDirectory = versionsDirectory?.Parent;
                if (versionDirectory == null || versionsDirectory == null || installDirectory == null ||
                    !string.Equals(payloadDirectory.Name, "payload", StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(versionsDirectory.Name, "versions", StringComparison.OrdinalIgnoreCase) ||
                    !TryParseVersionDirectory(versionDirectory.Name, out var manifestSha256))
                {
                    return false;
                }
                foreach (var directory in new[]
                {
                    installDirectory,
                    versionsDirectory,
                    versionDirectory,
                    payloadDirectory
                })
                {
                    if (!directory.Exists ||
                        (directory.Attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        return false;
                    }
                }

                var stateDirectory = new DirectoryInfo(Path.Combine(installDirectory.FullName, "state"));
                var startupDirectory = new DirectoryInfo(Path.Combine(stateDirectory.FullName, "startup"));
                if (!stateDirectory.Exists || !startupDirectory.Exists ||
                    (stateDirectory.Attributes & FileAttributes.ReparsePoint) != 0 ||
                    (startupDirectory.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    return false;
                }
                var authorizationPath = Path.GetFullPath(Path.Combine(
                    startupDirectory.FullName,
                    launchToken + ".launch"));
                var startupPrefix = Path.GetFullPath(startupDirectory.FullName)
                    .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (!authorizationPath.StartsWith(startupPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
                var expectedLauncherPath = Path.GetFullPath(Path.Combine(
                    installDirectory.FullName,
                    "Malco.Launcher.exe"));
                layout = new InstalledLaunchAuthorization(
                    manifestSha256,
                    authorizationPath,
                    versionDirectory.Name,
                    expectedLauncherPath,
                    false,
                    0,
                    0);
                return true;
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is NotSupportedException ||
                exception is ArgumentException)
            {
                return false;
            }
        }

        private static bool TryParseVersionDirectory(string name, out string manifestSha256)
        {
            manifestSha256 = null;
            if (name == null || name.Length != 85 || name[20] != '-' ||
                !name.Take(20).All(character => character >= '0' && character <= '9') ||
                !name.Substring(21).All(character =>
                    (character >= '0' && character <= '9') ||
                    (character >= 'a' && character <= 'f')))
            {
                return false;
            }
            if (!long.TryParse(
                    name.Substring(0, 20),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var sequence) || sequence <= 0)
            {
                return false;
            }
            manifestSha256 = name.Substring(21);
            return true;
        }
    }
}
