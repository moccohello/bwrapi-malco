using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Malco.Launcher
{
    /// <summary>
    /// Removes only paths Malco can still prove it owns. Signed release files
    /// are deleted by exact path, length, and digest; unknown, modified, and
    /// reparse-point content is deliberately preserved.
    /// </summary>
    internal static class OwnedInstallCleaner
    {
        internal static void CleanStagingDirectory(string stagingDirectory, ReleaseVerifier verifier)
        {
            if (!IsOrdinaryDirectory(stagingDirectory) ||
                !IsLowerHex(Path.GetFileName(stagingDirectory), 32)) return;
            var stagingMarker = Child(stagingDirectory, ".malco-staging-v1");
            if (!HasExactOrdinaryFile(stagingMarker, "malco-staging=1\r\n"))
            {
                DeleteOrdinaryFile(Child(stagingDirectory, ".malco-staging-v1.tmp"));
                DeleteIfEmptyOrdinaryDirectory(stagingDirectory);
                return;
            }
            DeleteOrdinaryFile(Child(stagingDirectory, ".malco-staging-v1.tmp"));
            var versionRoot = Child(stagingDirectory, "version");
            if (Directory.Exists(versionRoot) && !IsOrdinaryDirectory(versionRoot)) return;
            var envelopePath = Child(versionRoot, "release-envelope.json");
            DeleteOrdinaryFile(envelopePath + ".tmp");
            if (IsOrdinaryFile(envelopePath))
            {
                try
                {
                    var envelope = ReadOwnedEnvelope(envelopePath, verifier);
                    CleanExtractionPartials(
                        Child(stagingDirectory, "partials"),
                        envelope.Manifest.Files.Count);
                    CleanSignedPayload(Child(versionRoot, "payload"), envelope.Manifest.Files);
                    // The exact archive path is registered by the fixed
                    // write-ahead staging marker before download starts. A
                    // partial download is therefore still Malco-owned.
                    DeleteOrdinaryFile(Child(stagingDirectory, "payload.zip"));
                    DeleteOrdinaryFile(envelopePath);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException ||
                                           ex is InvalidDataException || ex is CryptographicException)
                {
                    // Keep the write-ahead marker so a later run still knows
                    // this is an incomplete Malco staging transaction.
                    return;
                }
            }
            DeleteIfEmptyOrdinaryDirectory(Child(versionRoot, "payload"));
            DeleteIfEmptyOrdinaryDirectory(versionRoot);
            DeleteIfEmptyOrdinaryDirectory(Child(stagingDirectory, "partials"));
            if (Directory.EnumerateFileSystemEntries(stagingDirectory)
                .Any(path => !string.Equals(path, stagingMarker, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }
            DeleteOrdinaryFile(stagingMarker);
            DeleteIfEmptyOrdinaryDirectory(stagingDirectory);
        }

        internal static void CleanSignedVersion(
            string versionRoot,
            ReleaseReference reference,
            ReleaseVerifier verifier)
        {
            if (!IsOrdinaryDirectory(versionRoot)) return;
            var envelopePath = Child(versionRoot, "release-envelope.json");
            if (!IsOrdinaryFile(envelopePath)) return;
            try
            {
                var envelope = ReadOwnedEnvelope(envelopePath, verifier);
                if (envelope.Manifest.Sequence != reference.Sequence ||
                    !string.Equals(envelope.ManifestSha256, reference.ManifestSha256, StringComparison.Ordinal))
                {
                    return;
                }
                CleanSignedPayload(Child(versionRoot, "payload"), envelope.Manifest.Files);
                DeleteOrdinaryFile(envelopePath);
                DeleteIfEmptyOrdinaryDirectory(Child(versionRoot, "payload"));
                DeleteIfEmptyOrdinaryDirectory(versionRoot);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException ||
                                       ex is InvalidDataException || ex is CryptographicException)
            {
                // Preserve a release when its ownership proof cannot be verified.
            }
        }

        private static void CleanSignedPayload(string payloadRoot, IReadOnlyList<ReleaseFile> files)
        {
            if (!IsOrdinaryDirectory(payloadRoot)) return;
            var root = Path.GetFullPath(payloadRoot).TrimEnd(Path.DirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            var parents = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in files)
            {
                var path = Path.GetFullPath(Path.Combine(
                    payloadRoot,
                    file.Path.Replace('/', Path.DirectorySeparatorChar)));
                if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("A signed uninstall path escapes the payload root.");
                }
                AddParents(parents, Path.GetDirectoryName(path), payloadRoot);
                if (!HasOnlyOrdinaryParents(path, payloadRoot) || !IsOrdinaryFile(path)) continue;
                DeleteMatchingFile(path, file.Length, file.Sha256);
            }
            foreach (var parent in parents.OrderByDescending(path => path.Length))
            {
                DeleteIfEmptyOrdinaryDirectory(parent);
            }
        }

        private static void CleanExtractionPartials(string partialRoot, int fileCount)
        {
            if (!IsOrdinaryDirectory(partialRoot)) return;
            for (var index = 0; index < fileCount; index++)
            {
                DeleteOrdinaryFile(Child(partialRoot, index.ToString("D8") + ".tmp"));
            }
            DeleteIfEmptyOrdinaryDirectory(partialRoot);
        }

        private static VerifiedEnvelope ReadOwnedEnvelope(string path, ReleaseVerifier verifier)
        {
            var envelopeBytes = ReleaseVerifier.ReadBoundedFile(path, verifier.MaximumEnvelopeBytes);
            if (verifier == null)
            {
                throw new InvalidDataException("Cleanup cannot verify release ownership without an approved policy.");
            }
            return verifier.VerifyEnvelope(envelopeBytes);
        }

        private static bool TryParseVersionDirectory(string name, out ReleaseReference reference)
        {
            reference = null;
            if (name == null || name.Length != 20 + 1 + 64 || name[20] != '-' ||
                !name.Take(20).All(character => character >= '0' && character <= '9') ||
                !IsLowerHex(name.Substring(21), 64))
            {
                return false;
            }
            long sequence;
            if (!long.TryParse(name.Substring(0, 20), NumberStyles.None, CultureInfo.InvariantCulture, out sequence) ||
                sequence <= 0)
            {
                return false;
            }
            reference = new ReleaseReference(sequence, name.Substring(21));
            return true;
        }

        private static void AddParents(HashSet<string> parents, string path, string root)
        {
            var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
            while (!string.IsNullOrEmpty(path) &&
                   !string.Equals(path, fullRoot, StringComparison.OrdinalIgnoreCase))
            {
                var fullPath = Path.GetFullPath(path);
                var rootPrefix = fullRoot + Path.DirectorySeparatorChar;
                if (!fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("A signed payload parent escapes its release root.");
                }
                parents.Add(fullPath);
                path = Path.GetDirectoryName(fullPath);
            }
        }

        private static bool HasOnlyOrdinaryParents(string path, string root)
        {
            var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
            var parent = Path.GetDirectoryName(Path.GetFullPath(path));
            while (!string.IsNullOrEmpty(parent))
            {
                if (!IsOrdinaryDirectory(parent)) return false;
                if (string.Equals(parent, fullRoot, StringComparison.OrdinalIgnoreCase)) return true;
                var rootPrefix = fullRoot + Path.DirectorySeparatorChar;
                if (!parent.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase)) return false;
                parent = Path.GetDirectoryName(parent);
            }
            return false;
        }

        private static string Child(string parent, string name)
        {
            var root = Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var child = Path.GetFullPath(Path.Combine(parent, name));
            if (!child.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("An uninstall path escapes the Malco-owned root.");
            }
            return child;
        }

        private static bool IsLowerHex(string value, int length)
        {
            return value != null && value.Length == length && value.All(character =>
                (character >= '0' && character <= '9') || (character >= 'a' && character <= 'f'));
        }

        private static bool IsOrdinaryFile(string path)
        {
            if (!File.Exists(path)) return false;
            var attributes = File.GetAttributes(path);
            return (attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) == 0;
        }

        private static bool HasExactOrdinaryFile(string path, string expectedAscii)
        {
            if (!IsOrdinaryFile(path)) return false;
            var bytes = File.ReadAllBytes(path);
            return CryptographicOperations.FixedTimeEquals(bytes, Encoding.ASCII.GetBytes(expectedAscii));
        }

        private static void DeleteMatchingFile(string path, long length, string sha256)
        {
            if (!IsOrdinaryFile(path)) return;
            var info = new FileInfo(path);
            if (info.Length == length &&
                string.Equals(ReleaseVerifier.Sha256File(path), sha256, StringComparison.Ordinal))
            {
                File.Delete(path);
            }
        }

        private static bool IsOrdinaryDirectory(string path)
        {
            if (!Directory.Exists(path)) return false;
            var attributes = File.GetAttributes(path);
            return (attributes & FileAttributes.ReparsePoint) == 0;
        }

        private static void RequireOrdinaryFile(string path)
        {
            if (!IsOrdinaryFile(path)) throw new InvalidDataException("A required uninstall file is missing or unsafe.");
        }

        private static void RequireOrdinaryDirectory(string path)
        {
            if (!IsOrdinaryDirectory(path)) throw new InvalidDataException("The uninstall root is missing or unsafe.");
        }

        private static void DeleteOrdinaryFile(string path)
        {
            if (IsOrdinaryFile(path)) File.Delete(path);
        }

        private static void DeleteIfEmptyOrdinaryDirectory(string path)
        {
            if (IsOrdinaryDirectory(path) && !Directory.EnumerateFileSystemEntries(path).Any())
            {
                Directory.Delete(path, false);
            }
        }
    }
}
