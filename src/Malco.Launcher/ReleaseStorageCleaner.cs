using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Malco.Launcher
{
    internal sealed class ReleaseStorageCleaner
    {
        private readonly string _versionsRoot;
        private readonly string _stagingRoot;
        private LauncherPolicy _policy;

        public ReleaseStorageCleaner(string versionsRoot, string stagingRoot)
        {
            _versionsRoot = versionsRoot;
            _stagingRoot = stagingRoot;
        }

        public void ConfigurePolicy(LauncherPolicy policy)
        {
            _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        }

        public string VersionDirectory(ReleaseReference reference)
        {
            if (reference == null || reference.Sequence <= 0 ||
                !ContractCodec.IsLowerSha256(reference.ManifestSha256))
            {
                throw new InvalidDataException("A release reference cannot identify a version directory.");
            }
            return LauncherPathGuard.Child(
                _versionsRoot,
                reference.Sequence.ToString("D20") + "-" + reference.ManifestSha256);
        }

        public void CleanupStaging(ReleaseVerifier verifier)
        {
            if (verifier == null) throw new ArgumentNullException(nameof(verifier));
            if (!Directory.Exists(_stagingRoot)) return;
            LauncherPathGuard.RequireOrdinaryDirectory(_stagingRoot, "staging root");
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
            LauncherPathGuard.RequireOrdinaryDirectory(_versionsRoot, "versions root");
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
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out sequence) || sequence <= 0)
            {
                return false;
            }
            reference = new ReleaseReference(sequence, name.Substring(21));
            return true;
        }
    }
}
