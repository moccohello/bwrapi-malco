using System;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Malco.Launcher
{
    internal static partial class ContractCodec
    {
        public const string ReleaseNotesFileName = "notes.json";
        public const int MaximumReleaseNotesBytes = 16 * 1024;
        private const int MaximumReleaseNoteLines = 30;
        private const int MaximumReleaseNoteTextLength = 6029;

        public static string TryReadReleaseNote(byte[] bytes, bool korean)
        {
            try
            {
                if (bytes == null ||
                    bytes.Length == 0 ||
                    bytes.Length > MaximumReleaseNotesBytes ||
                    (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF))
                {
                    return null;
                }

                using (var document = ParseDocument(bytes, 8))
                {
                    var root = RequireObject(document.RootElement, "release notes");
                    RequireProperties(root, "schema", "ko", "en");
                    RequireExactString(root, "schema", "malco.release-notes.v1");
                    var koreanText = RequireReleaseNoteText(root, "ko");
                    var englishText = RequireReleaseNoteText(root, "en");
                    return korean ? koreanText : englishText;
                }
            }
            catch (InvalidDataException)
            {
                return null;
            }
        }

        private static string RequireReleaseNoteText(JsonElement root, string name)
        {
            var text = RequireString(root, name, MaximumReleaseNoteTextLength);
            var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n').TrimEnd('\n');
            if (string.IsNullOrWhiteSpace(normalized) ||
                normalized.Any(character => character < 0x20 && character != '\n' && character != '\t'))
            {
                throw new InvalidDataException("The " + name + " release note is invalid.");
            }

            var lines = normalized.Split('\n');
            if (lines.Length > MaximumReleaseNoteLines)
            {
                throw new InvalidDataException("The " + name + " release note exceeds 30 lines.");
            }

            return normalized;
        }
    }
}
