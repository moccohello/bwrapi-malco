using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace Malco.Launcher
{
    internal sealed class StartupArtifactStore
    {
        private readonly string _startupRoot;
        private readonly Func<ReleaseReference, string> _versionDirectory;
        private LauncherPolicy _policy;

        public StartupArtifactStore(
            string startupRoot,
            Func<ReleaseReference, string> versionDirectory)
        {
            _startupRoot = startupRoot;
            _versionDirectory = versionDirectory ?? throw new ArgumentNullException(nameof(versionDirectory));
        }

        public void ConfigurePolicy(LauncherPolicy policy)
        {
            _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        }

        public string MarkerPath(string activationId)
        {
            if (!ContractCodec.IsActivationId(activationId))
            {
                throw new InvalidDataException("The startup activation ID is invalid.");
            }
            return LauncherPathGuard.Child(_startupRoot, activationId + ".json");
        }

        public string LaunchAuthorizationPath(string launchToken)
        {
            if (!ContractCodec.IsActivationId(launchToken))
            {
                throw new InvalidDataException("The launch authorization token is invalid.");
            }
            return LauncherPathGuard.Child(_startupRoot, launchToken + ".launch");
        }

        public void WriteLaunchAuthorization(
            string launchToken,
            ReleaseReference reference,
            bool requiredUpdateRecheck)
        {
            var versionDirectory = _versionDirectory(reference);
            var path = LaunchAuthorizationPath(launchToken);
            var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            using (var launcher = Process.GetCurrentProcess())
            {
                var bytes = System.Text.Encoding.ASCII.GetBytes(
                    "malco.launch-authorization.v2\r\n" +
                    Path.GetFileName(versionDirectory) + "\r\n" +
                    launcher.Id.ToString(CultureInfo.InvariantCulture) + "\r\n" +
                    launcher.StartTime.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture) + "\r\n" +
                    (requiredUpdateRecheck ? "1" : "0") + "\r\n");
                try
                {
                    using (var stream = new FileStream(
                        temporaryPath,
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None,
                        4096,
                        FileOptions.WriteThrough))
                    {
                        stream.Write(bytes, 0, bytes.Length);
                        stream.Flush(true);
                    }
                    File.Move(temporaryPath, path, false);
                }
                finally
                {
                    if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
                }
            }
        }

        public void DeleteLaunchAuthorization(string launchToken)
        {
            var path = LaunchAuthorizationPath(launchToken);
            if (File.Exists(path)) File.Delete(path);
        }

        public bool LaunchAuthorizationExists(string launchToken)
        {
            var path = LaunchAuthorizationPath(launchToken);
            try
            {
                var attributes = File.GetAttributes(path);
                if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
                {
                    throw new InvalidDataException("The launch authorization path is not an ordinary file.");
                }
                return true;
            }
            catch (FileNotFoundException)
            {
                return false;
            }
            catch (DirectoryNotFoundException)
            {
                return false;
            }
        }

        public void CleanupLaunchAuthorizations()
        {
            foreach (var path in Directory.EnumerateFiles(_startupRoot, "*.launch.*.tmp", SearchOption.TopDirectoryOnly))
            {
                var name = Path.GetFileName(path);
                if (name.Length == 108 &&
                    name.Substring(64, 8) == ".launch." &&
                    name.EndsWith(".tmp", StringComparison.Ordinal) &&
                    ContractCodec.IsActivationId(name.Substring(0, 64)) &&
                    name.Substring(72, 32).All(character =>
                        (character >= '0' && character <= '9') ||
                        (character >= 'a' && character <= 'f')))
                {
                    LauncherPathGuard.RequireOrdinaryFile(
                        path,
                        "stale launch-authorization temporary file");
                    File.Delete(path);
                }
            }
            foreach (var path in Directory.EnumerateFiles(_startupRoot, "*.launch", SearchOption.TopDirectoryOnly))
            {
                var name = Path.GetFileName(path);
                if (name.Length != 71 ||
                    !name.EndsWith(".launch", StringComparison.Ordinal) ||
                    !ContractCodec.IsActivationId(name.Substring(0, 64)))
                {
                    continue;
                }
                LauncherPathGuard.RequireOrdinaryFile(path, "stale launch authorization");
                File.Delete(path);
            }
        }

        public StartupMarker TryReadMarker(string activationId)
        {
            var path = MarkerPath(activationId);
            if (!File.Exists(path)) return null;
            RequirePolicy();
            var marker = ContractCodec.ParseStartupMarker(
                ReleaseVerifier.ReadBoundedFile(path, _policy.MaximumStartupMarkerBytes),
                _policy);
            if (!string.Equals(marker.ActivationId, activationId, StringComparison.Ordinal))
            {
                throw new InvalidDataException("The startup marker does not match its activation.");
            }
            return marker;
        }

        public void DeleteMarker(string activationId)
        {
            var path = MarkerPath(activationId);
            if (File.Exists(path)) File.Delete(path);
        }

        public string NewActivationId()
        {
            var bytes = new byte[32];
            using (var random = RandomNumberGenerator.Create())
            {
                random.GetBytes(bytes);
            }
            return BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
        }

        private void RequirePolicy()
        {
            if (_policy == null)
            {
                throw new InvalidOperationException("The launcher policy must be configured before state access.");
            }
        }
    }
}
