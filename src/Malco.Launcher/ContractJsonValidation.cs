using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Malco.Launcher
{
    internal static partial class ContractCodec
    {
        private const int HardMaximumEnvelopeBytes = 16 * 1024 * 1024;
        private const int HardMaximumSignedBytes = 12 * 1024 * 1024;
        private const int HardMaximumStateBytes = 4 * 1024 * 1024;
        private const int HardMaximumFiles = 65536;
        private const long HardMaximumArchiveBytes = 524288000L;
        private const long HardMaximumPayloadBytes = 16L * 1024L * 1024L * 1024L;
        private static readonly Regex LowerSha256 = new Regex(
            "^[0-9a-f]{64}$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);
        private static readonly Regex ActivationId = new Regex(
            "^[0-9a-f]{64}$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        public static string RequireCanonicalRelativePath(string path, bool allowDirectory)
        {
            if (string.IsNullOrEmpty(path) || path.Length > 512 || path[0] == '/' ||
                path.IndexOf('\\') >= 0 || path.IndexOf(':') >= 0 || path.Contains("//") ||
                path.IndexOfAny(new[] { '*', '?', '"', '<', '>', '|' }) >= 0 ||
                path.IndexOf('\0') >= 0 || (!allowDirectory && path.EndsWith("/", StringComparison.Ordinal)))
            {
                throw new InvalidDataException("The release contains a non-canonical path: " + path);
            }
            var candidate = allowDirectory ? path.TrimEnd('/') : path;
            if (candidate.Length == 0)
            {
                throw new InvalidDataException("The release contains an empty path.");
            }
            foreach (var segment in candidate.Split('/'))
            {
                if (segment.Length == 0 || segment.Length > 255 ||
                    segment == "." || segment == ".." ||
                    segment.EndsWith(".", StringComparison.Ordinal) ||
                    segment.EndsWith(" ", StringComparison.Ordinal) ||
                    segment.Any(character => character < 0x20) || IsReservedWindowsName(segment))
                {
                    throw new InvalidDataException("The release contains an unsafe Windows path: " + path);
                }
            }
            return allowDirectory && path.EndsWith("/", StringComparison.Ordinal)
                ? candidate + "/"
                : candidate;
        }

        public static bool IsLowerSha256(string value) =>
            value != null && LowerSha256.IsMatch(value);

        public static bool IsActivationId(string value) =>
            value != null && ActivationId.IsMatch(value);

        private static JsonDocument ParseDocument(byte[] bytes, int maximumDepth)
        {
            try
            {
                return JsonDocument.Parse(bytes, new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = maximumDepth
                });
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException("A JSON contract is malformed.", ex);
            }
        }

        private static JsonElement RequireObject(JsonElement value, string label)
        {
            if (value.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException("The " + label + " value must be an object.");
            }
            return value;
        }

        private static void RequireProperties(JsonElement value, params string[] requiredNames)
        {
            var actual = value.EnumerateObject().Select(property => property.Name).ToArray();
            if (actual.Length != requiredNames.Length ||
                actual.Distinct(StringComparer.Ordinal).Count() != actual.Length ||
                requiredNames.Any(name => !actual.Contains(name, StringComparer.Ordinal)))
            {
                throw new InvalidDataException("A JSON contract contains missing, duplicate, or unknown properties.");
            }
        }

        private static JsonElement RequireProperty(JsonElement value, string name)
        {
            JsonElement property;
            if (!value.TryGetProperty(name, out property))
            {
                throw new InvalidDataException("A JSON contract is missing property: " + name);
            }
            return property;
        }

        private static string RequireString(JsonElement value, string name, int maximumLength)
        {
            var property = RequireProperty(value, name);
            if (property.ValueKind != JsonValueKind.String)
            {
                throw new InvalidDataException("The " + name + " value must be a string.");
            }
            var text = property.GetString();
            if (string.IsNullOrEmpty(text) || text.Length > maximumLength)
            {
                throw new InvalidDataException("The " + name + " string has an invalid length.");
            }
            return text;
        }

        private static void RequireExactString(JsonElement value, string name, string expected)
        {
            if (!string.Equals(RequireString(value, name, expected.Length), expected, StringComparison.Ordinal))
            {
                throw new InvalidDataException("The " + name + " value is not supported.");
            }
        }

        private static long RequireInt64(JsonElement value, string name, long minimum, long maximum)
        {
            var property = RequireProperty(value, name);
            long result;
            if (property.ValueKind != JsonValueKind.Number || !property.TryGetInt64(out result) ||
                result < minimum || result > maximum)
            {
                throw new InvalidDataException("The " + name + " integer is outside the accepted range.");
            }
            return result;
        }

        private static string RequireSha256(JsonElement value, string name)
        {
            var text = RequireString(value, name, 64);
            if (!LowerSha256.IsMatch(text))
            {
                throw new InvalidDataException("The " + name + " value must be lowercase SHA-256 hex.");
            }
            return text;
        }

        private static Uri ParseHttpsUri(string text, string label)
        {
            Uri uri;
            if (!Uri.TryCreate(text, UriKind.Absolute, out uri) ||
                !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrEmpty(uri.Host) || !string.IsNullOrEmpty(uri.UserInfo) ||
                !string.IsNullOrEmpty(uri.Fragment))
            {
                throw new InvalidDataException("The " + label + " must be an absolute HTTPS URL.");
            }
            return uri;
        }

        private static byte[] DecodeBase64(string text, string label)
        {
            try
            {
                return Convert.FromBase64String(text);
            }
            catch (FormatException ex)
            {
                throw new InvalidDataException("The " + label + " is not valid base64.", ex);
            }
        }

        private static void RequireBounded(byte[] bytes, int maximum, string label)
        {
            if (bytes == null || bytes.Length == 0 || bytes.Length > maximum)
            {
                throw new InvalidDataException("The " + label + " has an invalid size.");
            }
        }

        private static bool IsReservedWindowsName(string segment)
        {
            var stem = segment.Split('.')[0];
            if (string.Equals(stem, "CON", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(stem, "PRN", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(stem, "AUX", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(stem, "NUL", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (stem.Length == 4 &&
                (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase) ||
                 stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) &&
                ((stem[3] >= '1' && stem[3] <= '9') ||
                 stem[3] == '\u00B9' || stem[3] == '\u00B2' || stem[3] == '\u00B3'))
            {
                return true;
            }
            return false;
        }
    }
}
