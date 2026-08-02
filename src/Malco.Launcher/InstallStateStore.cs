using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace Malco.Launcher
{
    internal sealed class InstallStateStore
    {
        private readonly string _installRoot;
        private readonly string _stateRoot;
        private readonly string _versionsRoot;
        private readonly string _stagingRoot;
        private readonly string _startupRoot;
        private readonly string _statePath;
        private LauncherPolicy _policy;
        private long? _lastSavedGeneration;

        public InstallStateStore(string installRoot)
        {
            _installRoot = Path.GetFullPath(installRoot).TrimEnd(Path.DirectorySeparatorChar);
            _stateRoot = Child(_installRoot, "state");
            _versionsRoot = Child(_installRoot, "versions");
            _stagingRoot = Child(_installRoot, "staging");
            _startupRoot = Child(_stateRoot, "startup");
            _statePath = Child(_stateRoot, "install-state.json");
        }

        public string PolicyPath => Child(_installRoot, "launcher-policy.json");
        public string StagingRoot => _stagingRoot;
        public string StagedUpdatePath => Child(_stateRoot, "staged-update.json");

        public void ConfigurePolicy(LauncherPolicy policy)
        {
            _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        }

        public void EnsureRoots()
        {
            RequireOrdinaryDirectory(_installRoot, "install root");
            var markerPath = Child(_installRoot, ".malco-install-root");
            RequireOrdinaryFile(markerPath, "install-root marker");
            var marker = File.ReadAllBytes(markerPath);
            var expectedMarker = System.Text.Encoding.ASCII.GetBytes("malco-install-root=1\r\n");
            if (!CryptographicOperations.FixedTimeEquals(marker, expectedMarker))
            {
                throw new InvalidDataException("The launcher is not running from an owned Malco install root.");
            }
            Directory.CreateDirectory(_stateRoot);
            Directory.CreateDirectory(_versionsRoot);
            Directory.CreateDirectory(_stagingRoot);
            Directory.CreateDirectory(_startupRoot);
            RequireOrdinaryDirectory(_stateRoot, "state root");
            RequireOrdinaryDirectory(_versionsRoot, "versions root");
            RequireOrdinaryDirectory(_stagingRoot, "staging root");
            RequireOrdinaryDirectory(_startupRoot, "startup-marker root");
            CleanupLaunchAuthorizations();
        }

        public InstallState Load()
        {
            RequirePolicy();
            if (!File.Exists(_statePath))
            {
                throw new InvalidDataException("The installed launcher state is missing; repair or reinstall Malco.");
            }
            return ContractCodec.ParseState(
                ReleaseVerifier.ReadBoundedFile(_statePath, _policy.MaximumStateBytes),
                _policy);
        }

        public void Save(InstallState state)
        {
            RequirePolicy();
            if (state == null) throw new ArgumentNullException(nameof(state));
            var generation = state.Generation;
            if (_lastSavedGeneration.HasValue && _lastSavedGeneration.Value > generation)
            {
                generation = _lastSavedGeneration.Value;
            }
            if (generation == long.MaxValue)
            {
                throw new InvalidDataException("The install-state generation is exhausted.");
            }
            var persistedState = new InstallState(
                generation + 1,
                state.HighestAcceptedSequence,
                state.Current,
                state.LastKnownGood,
                state.Pending,
                state.LastRollback);
            var bytes = ContractCodec.SerializeState(persistedState);
            var temporaryPath = _statePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
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
                ContractCodec.ParseState(
                    ReleaseVerifier.ReadBoundedFile(temporaryPath, _policy.MaximumStateBytes),
                    _policy);
                if (File.Exists(_statePath))
                {
                    File.Replace(temporaryPath, _statePath, null, true);
                }
                else
                {
                    File.Move(temporaryPath, _statePath, false);
                }
                _lastSavedGeneration = persistedState.Generation;
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
        }

        public string VersionDirectory(ReleaseReference reference)
        {
            if (reference == null || reference.Sequence <= 0 ||
                !ContractCodec.IsLowerSha256(reference.ManifestSha256))
            {
                throw new InvalidDataException("A release reference cannot identify a version directory.");
            }
            return Child(
                _versionsRoot,
                reference.Sequence.ToString("D20") + "-" + reference.ManifestSha256);
        }

        public string MarkerPath(string activationId)
        {
            if (!ContractCodec.IsActivationId(activationId))
            {
                throw new InvalidDataException("The startup activation ID is invalid.");
            }
            return Child(_startupRoot, activationId + ".json");
        }

        public string LaunchAuthorizationPath(string launchToken)
        {
            if (!ContractCodec.IsActivationId(launchToken))
            {
                throw new InvalidDataException("The launch authorization token is invalid.");
            }
            return Child(_startupRoot, launchToken + ".launch");
        }

        public void WriteLaunchAuthorization(
            string launchToken,
            ReleaseReference reference,
            bool requiredUpdateRecheck)
        {
            var versionDirectory = VersionDirectory(reference);
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

        private void CleanupLaunchAuthorizations()
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
                    RequireOrdinaryFile(path, "stale launch-authorization temporary file");
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
                RequireOrdinaryFile(path, "stale launch authorization");
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

        public void CleanupStaging(ReleaseVerifier verifier)
        {
            if (verifier == null) throw new ArgumentNullException(nameof(verifier));
            if (!Directory.Exists(_stagingRoot)) return;
            RequireOrdinaryDirectory(_stagingRoot, "staging root");
            foreach (var entry in Directory.EnumerateFileSystemEntries(_stagingRoot))
            {
                OwnedInstallCleaner.CleanStagingDirectory(entry, verifier);
            }
        }

        public void CleanupUnreferencedVersions(
            InstallState state,
            ReleaseVerifier verifier,
            Func<ReleaseReference, bool> isVersionInUse,
            ReleaseReference stagedUpdate = null)
        {
            RequirePolicy();
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (verifier == null) throw new ArgumentNullException(nameof(verifier));
            if (isVersionInUse == null) throw new ArgumentNullException(nameof(isVersionInUse));
            if (!Directory.Exists(_versionsRoot)) return;
            RequireOrdinaryDirectory(_versionsRoot, "versions root");
            var retained = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddRetained(retained, state.Current);
            AddRetained(retained, state.LastKnownGood);
            if (stagedUpdate != null)
            {
                if (_policy.RetainedStagedUpdateCount < 1)
                {
                    throw new InvalidDataException("The launcher policy does not permit retaining a staged update.");
                }
                AddRetained(retained, stagedUpdate);
            }
            if (state.Pending != null)
            {
                AddRetained(retained, state.Pending.Candidate);
                AddRetained(retained, state.Pending.PreviousCurrent);
            }

            foreach (var entry in Directory.EnumerateFileSystemEntries(_versionsRoot))
            {
                var fullPath = Path.GetFullPath(entry);
                if (!retained.Contains(fullPath))
                {
                    ReleaseReference reference;
                    if (Directory.Exists(fullPath) &&
                        (File.GetAttributes(fullPath) & FileAttributes.ReparsePoint) == 0 &&
                        TryParseVersionDirectory(Path.GetFileName(fullPath), out reference))
                    {
                        if (isVersionInUse(reference)) continue;
                        OwnedInstallCleaner.CleanSignedVersion(fullPath, reference, verifier);
                    }
                }
            }
        }

        private void AddRetained(HashSet<string> retained, ReleaseReference reference)
        {
            if (reference != null) retained.Add(Path.GetFullPath(VersionDirectory(reference)));
        }

        private void RequirePolicy()
        {
            if (_policy == null)
            {
                throw new InvalidOperationException("The launcher policy must be configured before state access.");
            }
        }

        private static string Child(string parent, string name)
        {
            var root = Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var child = Path.GetFullPath(Path.Combine(parent, name));
            if (!child.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("A launcher path escapes its fixed install root.");
            }
            return child;
        }

        private static bool TryParseVersionDirectory(string name, out ReleaseReference reference)
        {
            reference = null;
            if (name == null || name.Length != 85 || name[20] != '-' ||
                !name.Take(20).All(character => character >= '0' && character <= '9') ||
                !name.Substring(21).All(character =>
                    (character >= '0' && character <= '9') || (character >= 'a' && character <= 'f')))
            {
                return false;
            }
            long sequence;
            if (!long.TryParse(
                    name.Substring(0, 20),
                    System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out sequence) || sequence <= 0)
            {
                return false;
            }
            reference = new ReleaseReference(sequence, name.Substring(21));
            return true;
        }

        private static void RequireOrdinaryDirectory(string path, string label)
        {
            var attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.Directory) == 0 ||
                (attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException("The launcher " + label + " is not an ordinary directory.");
            }
        }

        private static void RequireOrdinaryFile(string path, string label)
        {
            if (!File.Exists(path))
            {
                throw new InvalidDataException("The launcher " + label + " is missing.");
            }
            var attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.Directory) != 0 ||
                (attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException("The launcher " + label + " is not an ordinary file.");
            }
        }
    }
}
