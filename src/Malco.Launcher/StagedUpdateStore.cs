using System;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Malco.Launcher
{
    internal sealed class StagedUpdateStore
    {
        private const string Schema = "malco.staged-update.v1";

        private readonly InstallStateStore _stateStore;
        private readonly LauncherPolicy _policy;

        public StagedUpdateStore(InstallStateStore stateStore, LauncherPolicy policy)
        {
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
            _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        }

        public ReleaseReference TryLoad()
        {
            var path = _stateStore.StagedUpdatePath;
            if (!File.Exists(path)) return null;
            var bytes = ReleaseVerifier.ReadBoundedFile(path, _policy.MaximumStagedUpdateBytes);
            try
            {
                using (var document = JsonDocument.Parse(bytes, new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = 4
                }))
                {
                    var root = document.RootElement;
                    RequireObject(root, "staged update");
                    RequireProperties(root, "schema", "candidate");
                    var schema = RequireString(root, "schema", Schema.Length);
                    if (!string.Equals(schema, Schema, StringComparison.Ordinal))
                    {
                        throw new InvalidDataException("The staged-update schema is not supported.");
                    }
                    var candidate = root.GetProperty("candidate");
                    RequireObject(candidate, "staged candidate");
                    RequireProperties(candidate, "sequence", "manifest_sha256");
                    long sequence;
                    if (!candidate.GetProperty("sequence").TryGetInt64(out sequence) || sequence <= 0)
                    {
                        throw new InvalidDataException("The staged-update sequence is invalid.");
                    }
                    var manifestSha256 = RequireString(candidate, "manifest_sha256", 64);
                    if (!ContractCodec.IsLowerSha256(manifestSha256))
                    {
                        throw new InvalidDataException("The staged-update manifest identity is invalid.");
                    }
                    return new ReleaseReference(sequence, manifestSha256);
                }
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException("The staged-update record is malformed.", ex);
            }
        }

        public void Save(ReleaseReference reference)
        {
            if (reference == null || reference.Sequence <= 0 ||
                !ContractCodec.IsLowerSha256(reference.ManifestSha256))
            {
                throw new InvalidDataException("The staged-update reference is invalid.");
            }
            byte[] bytes;
            using (var stream = new MemoryStream())
            {
                using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
                {
                    writer.WriteStartObject();
                    writer.WriteString("schema", Schema);
                    writer.WritePropertyName("candidate");
                    writer.WriteStartObject();
                    writer.WriteNumber("sequence", reference.Sequence);
                    writer.WriteString("manifest_sha256", reference.ManifestSha256);
                    writer.WriteEndObject();
                    writer.WriteEndObject();
                }
                bytes = stream.ToArray();
            }
            if (bytes.Length > _policy.MaximumStagedUpdateBytes)
            {
                throw new InvalidDataException("The staged-update record exceeds its policy limit.");
            }

            var path = _stateStore.StagedUpdatePath;
            var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var output = new FileStream(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    4096,
                    FileOptions.WriteThrough))
                {
                    output.Write(bytes, 0, bytes.Length);
                    output.Flush(true);
                }
                ReleaseVerifier.ReadBoundedFile(temporaryPath, _policy.MaximumStagedUpdateBytes);
                if (File.Exists(path)) File.Replace(temporaryPath, path, null, true);
                else File.Move(temporaryPath, path, false);
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
        }

        public void Delete()
        {
            var path = _stateStore.StagedUpdatePath;
            if (!File.Exists(path)) return;
            var attributes = File.GetAttributes(path);
            if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
            {
                throw new InvalidDataException("The staged-update record is not an ordinary file.");
            }
            File.Delete(path);
        }

        private static void RequireObject(JsonElement value, string label)
        {
            if (value.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException("The " + label + " must be an object.");
            }
        }

        private static void RequireProperties(JsonElement value, params string[] expected)
        {
            var actual = value.EnumerateObject().Select(property => property.Name).ToArray();
            if (actual.Length != expected.Length ||
                actual.Distinct(StringComparer.Ordinal).Count() != actual.Length ||
                expected.Any(name => !actual.Contains(name, StringComparer.Ordinal)))
            {
                throw new InvalidDataException("The staged-update record has missing, duplicate, or unknown properties.");
            }
        }

        private static string RequireString(JsonElement value, string name, int maximumLength)
        {
            JsonElement property;
            if (!value.TryGetProperty(name, out property) || property.ValueKind != JsonValueKind.String)
            {
                throw new InvalidDataException("The staged-update " + name + " must be a string.");
            }
            var result = property.GetString();
            if (string.IsNullOrEmpty(result) || result.Length > maximumLength)
            {
                throw new InvalidDataException("The staged-update " + name + " has an invalid length.");
            }
            return result;
        }
    }
}
