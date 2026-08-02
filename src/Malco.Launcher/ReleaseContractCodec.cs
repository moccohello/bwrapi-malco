using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Malco.Launcher
{
    internal static partial class ContractCodec
    {
        private static readonly string[] RequiredReleaseFiles =
        {
            "Malco.exe",
            "Malco.dll",
            "Malco.runtimeconfig.json",
            "Malco.Telemetry.dll",
            "telemetry-policy.json",
            "BwrApi.Client.dll",
            "MALCO-PACKAGE-BOM.json",
            "RUNTIME-CONTRACT.json",
            "LICENSE.txt",
            "bwrapi/runtime/win-x64/bwrapi_runtime.dll",
            "bwrapi/runtime/win-x64/LICENSE.runtime.txt",
            "bwrapi/runtime/win-x64/THIRD_PARTY_NOTICES.md"
        };

        public static LauncherPolicy ParsePolicy(byte[] bytes)
        {
            RequireBounded(bytes, LauncherPolicy.BootstrapMaximumPolicyBytes, "launcher policy");
            using (var document = ParseDocument(bytes, 8))
            {
                var root = RequireObject(document.RootElement, "launcher policy");
                RequireProperties(
                    root,
                    "schema",
                    "feed_url",
                    "public_key_spki",
                    "maximum_policy_bytes",
                    "maximum_envelope_bytes",
                    "maximum_signed_manifest_bytes",
                    "maximum_state_bytes",
                    "maximum_startup_marker_bytes",
                    "maximum_staged_update_bytes",
                    "maximum_files",
                    "maximum_archive_bytes",
                    "maximum_payload_bytes",
                    "maximum_archive_entries",
                    "feed_timeout_ms",
                    "archive_timeout_ms",
                    "launcher_coordination_timeout_ms",
                    "startup_timeout_ms",
                    "authorization_timeout_ms",
                    "termination_timeout_ms",
                    "startup_poll_ms",
                    "authorization_poll_ms",
                    "selected_release_stability_ms",
                    "retained_staged_update_count");
                RequireExactString(root, "schema", "malco.launcher-policy.v2");
                var feedUri = ParseHttpsUri(RequireString(root, "feed_url", 2048), "feed URL");
                var keyText = RequireString(root, "public_key_spki", 4096);
                byte[] keyBytes;
                try
                {
                    keyBytes = Convert.FromBase64String(keyText);
                }
                catch (FormatException ex)
                {
                    throw new InvalidDataException("The launcher policy public key is not valid base64.", ex);
                }
                if (keyBytes.Length < 64 || keyBytes.Length > 512)
                {
                    throw new InvalidDataException("The launcher policy public key has an invalid size.");
                }
                var policy = new LauncherPolicy
                {
                    FeedUri = feedUri,
                    PublicKeySubjectPublicKeyInfo = keyBytes,
                    MaximumPolicyBytes = checked((int)RequireInt64(root, "maximum_policy_bytes", 4096, LauncherPolicy.BootstrapMaximumPolicyBytes)),
                    MaximumEnvelopeBytes = checked((int)RequireInt64(root, "maximum_envelope_bytes", 4096, HardMaximumEnvelopeBytes)),
                    MaximumSignedManifestBytes = checked((int)RequireInt64(root, "maximum_signed_manifest_bytes", 4096, HardMaximumSignedBytes)),
                    MaximumStateBytes = checked((int)RequireInt64(root, "maximum_state_bytes", 4096, HardMaximumStateBytes)),
                    MaximumStartupMarkerBytes = checked((int)RequireInt64(root, "maximum_startup_marker_bytes", 256, 64 * 1024)),
                    MaximumStagedUpdateBytes = checked((int)RequireInt64(root, "maximum_staged_update_bytes", 256, 64 * 1024)),
                    MaximumFiles = checked((int)RequireInt64(
                        root,
                        "maximum_files",
                        RequiredReleaseFiles.Length,
                        HardMaximumFiles)),
                    MaximumArchiveBytes = RequireInt64(root, "maximum_archive_bytes", 1, HardMaximumArchiveBytes),
                    MaximumPayloadBytes = RequireInt64(
                        root,
                        "maximum_payload_bytes",
                        RequiredReleaseFiles.Length,
                        HardMaximumPayloadBytes),
                    MaximumArchiveEntries = checked((int)RequireInt64(
                        root,
                        "maximum_archive_entries",
                        RequiredReleaseFiles.Length,
                        HardMaximumFiles * 2L)),
                    FeedTimeoutMilliseconds = checked((int)RequireInt64(root, "feed_timeout_ms", 100, 10 * 60 * 1000)),
                    ArchiveTimeoutMilliseconds = checked((int)RequireInt64(root, "archive_timeout_ms", 100, 60 * 60 * 1000)),
                    LauncherCoordinationTimeoutMilliseconds = checked((int)RequireInt64(root, "launcher_coordination_timeout_ms", 100, 10 * 60 * 1000)),
                    StartupTimeoutMilliseconds = checked((int)RequireInt64(root, "startup_timeout_ms", 100, 10 * 60 * 1000)),
                    AuthorizationTimeoutMilliseconds = checked((int)RequireInt64(root, "authorization_timeout_ms", 100, 10 * 60 * 1000)),
                    TerminationTimeoutMilliseconds = checked((int)RequireInt64(root, "termination_timeout_ms", 100, 10 * 60 * 1000)),
                    StartupPollMilliseconds = checked((int)RequireInt64(root, "startup_poll_ms", 10, 10 * 1000)),
                    AuthorizationPollMilliseconds = checked((int)RequireInt64(root, "authorization_poll_ms", 10, 10 * 1000)),
                    SelectedReleaseStabilityMilliseconds = checked((int)RequireInt64(root, "selected_release_stability_ms", 10, 10 * 1000)),
                    RetainedStagedUpdateCount = checked((int)RequireInt64(root, "retained_staged_update_count", 1, 1))
                };
                if (bytes.Length > policy.MaximumPolicyBytes ||
                    policy.MaximumSignedManifestBytes > policy.MaximumEnvelopeBytes ||
                    policy.MaximumArchiveBytes > policy.MaximumPayloadBytes ||
                    policy.StartupPollMilliseconds > policy.StartupTimeoutMilliseconds ||
                    policy.AuthorizationPollMilliseconds > policy.AuthorizationTimeoutMilliseconds ||
                    policy.SelectedReleaseStabilityMilliseconds > policy.AuthorizationTimeoutMilliseconds)
                {
                    throw new InvalidDataException("The launcher policy limits are internally inconsistent.");
                }
                return policy;
            }
        }

        public static EnvelopeParts ParseEnvelope(byte[] bytes, LauncherPolicy policy)
        {
            if (policy == null) throw new ArgumentNullException(nameof(policy));
            RequireBounded(bytes, policy.MaximumEnvelopeBytes, "release envelope");
            using (var document = ParseDocument(bytes, 8))
            {
                var root = RequireObject(document.RootElement, "release envelope");
                RequireProperties(root, "schema", "signed", "signature");
                RequireExactString(root, "schema", "malco.signed-release-envelope.v1");
                var signed = DecodeBase64(RequireString(root, "signed", policy.MaximumSignedManifestBytes * 2), "signed payload");
                var signature = DecodeBase64(RequireString(root, "signature", 512), "signature");
                if (signed.Length == 0 || signed.Length > policy.MaximumSignedManifestBytes)
                {
                    throw new InvalidDataException("The signed release payload has an invalid size.");
                }
                if (signature.Length != 64)
                {
                    throw new InvalidDataException("The ECDSA P-256 P1363 signature must be exactly 64 bytes.");
                }
                var canonical = SerializeCanonicalEnvelope(signed, signature);
                if (!bytes.SequenceEqual(canonical))
                {
                    throw new InvalidDataException("The release envelope is not in its exact canonical encoding.");
                }
                return new EnvelopeParts { SignedBytes = signed, SignatureBytes = signature };
            }
        }

        private static byte[] SerializeCanonicalEnvelope(byte[] signed, byte[] signature)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
                {
                    writer.WriteStartObject();
                    writer.WriteString("schema", "malco.signed-release-envelope.v1");
                    writer.WriteString("signed", Convert.ToBase64String(signed));
                    writer.WriteString("signature", Convert.ToBase64String(signature));
                    writer.WriteEndObject();
                }
                return stream.ToArray();
            }
        }

        public static ReleaseManifest ParseReleaseManifest(byte[] bytes, LauncherPolicy policy)
        {
            if (policy == null) throw new ArgumentNullException(nameof(policy));
            RequireBounded(bytes, policy.MaximumSignedManifestBytes, "signed release payload");
            using (var document = ParseDocument(bytes, 16))
            {
                var root = RequireObject(document.RootElement, "signed release payload");
                RequireProperties(
                    root,
                    "schema",
                    "product",
                    "platform",
                    "sequence",
                    "version",
                    "update_policy",
                    "components",
                    "archive",
                    "files");
                RequireExactString(root, "schema", "malco.release.v2");
                RequireExactString(root, "product", "Malco");
                RequireExactString(root, "platform", "win-x64");
                var sequence = RequireInt64(root, "sequence", 1, long.MaxValue);
                var version = RequireString(root, "version", 128);
                var updatePolicy = RequireString(root, "update_policy", 8);
                if (!string.Equals(updatePolicy, "optional", StringComparison.Ordinal) &&
                    !string.Equals(updatePolicy, "required", StringComparison.Ordinal))
                {
                    throw new InvalidDataException("The update_policy value is not supported.");
                }

                var components = RequireObject(RequireProperty(root, "components"), "components");
                RequireProperties(components, "malco_version", "bwrapi_version");
                var malcoVersion = RequireString(components, "malco_version", 128);
                RequireString(components, "bwrapi_version", 128);
                if (!string.Equals(version, malcoVersion, StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        "The release version must match components.malco_version.");
                }

                var archiveObject = RequireObject(RequireProperty(root, "archive"), "archive");
                RequireProperties(archiveObject, "url", "length", "sha256");
                var archive = new ReleaseArchive
                {
                    Uri = ParseHttpsUri(RequireString(archiveObject, "url", 2048), "archive URL"),
                    Length = RequireInt64(archiveObject, "length", 1, policy.MaximumArchiveBytes),
                    Sha256 = RequireSha256(archiveObject, "sha256")
                };

                var filesElement = RequireProperty(root, "files");
                if (filesElement.ValueKind != JsonValueKind.Array)
                {
                    throw new InvalidDataException("The release files value must be an array.");
                }
                var files = new List<ReleaseFile>();
                var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                long totalLength = 0;
                string previousPath = null;
                foreach (var item in filesElement.EnumerateArray())
                {
                    if (files.Count >= policy.MaximumFiles)
                    {
                        throw new InvalidDataException("The release declares too many files.");
                    }
                    var fileObject = RequireObject(item, "release file");
                    RequireProperties(fileObject, "path", "length", "sha256");
                    var path = RequireCanonicalRelativePath(RequireString(fileObject, "path", 512), false);
                    if (!paths.Add(path))
                    {
                        throw new InvalidDataException("The release contains a duplicate file path: " + path);
                    }
                    if (previousPath != null && StringComparer.Ordinal.Compare(previousPath, path) >= 0)
                    {
                        throw new InvalidDataException("Release files must be strictly sorted by ordinal path.");
                    }
                    previousPath = path;
                    var length = RequireInt64(fileObject, "length", 0, policy.MaximumArchiveBytes);
                    if (totalLength > policy.MaximumPayloadBytes - length)
                    {
                        throw new InvalidDataException("The declared release payload is too large.");
                    }
                    totalLength += length;
                    files.Add(new ReleaseFile
                    {
                        Path = path,
                        Length = length,
                        Sha256 = RequireSha256(fileObject, "sha256")
                    });
                }
                if (files.Count == 0)
                {
                    throw new InvalidDataException("The release file set is empty.");
                }
                RequireNoFileAncestorConflicts(paths);
                RequireReleaseFiles(files);

                return new ReleaseManifest
                {
                    Sequence = sequence,
                    Version = version,
                    UpdatePolicy = updatePolicy,
                    Archive = archive,
                    Files = files
                };
            }
        }

        private static void RequireReleaseFiles(IReadOnlyList<ReleaseFile> files)
        {
            var byPath = files.ToDictionary(file => file.Path, StringComparer.OrdinalIgnoreCase);
            foreach (var required in RequiredReleaseFiles)
            {
                ReleaseFile file;
                if (!byPath.TryGetValue(required, out file) ||
                    !string.Equals(file.Path, required, StringComparison.Ordinal) ||
                    file.Length <= 0)
                {
                    throw new InvalidDataException(
                        "The release does not bind a required payload file exactly: " + required);
                }
            }
        }

        private static void RequireNoFileAncestorConflicts(IReadOnlySet<string> paths)
        {
            foreach (var path in paths)
            {
                var separator = path.LastIndexOf('/');
                while (separator >= 0)
                {
                    var ancestor = path.Substring(0, separator);
                    if (paths.Contains(ancestor))
                    {
                        throw new InvalidDataException(
                            "A release file path is also an ancestor of another file: " + ancestor);
                    }
                    separator = ancestor.LastIndexOf('/');
                }
            }
        }
    }
}
