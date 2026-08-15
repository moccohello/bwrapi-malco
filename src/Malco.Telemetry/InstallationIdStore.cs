using System;
using System.IO;
using System.Text;

namespace Malco.Telemetry
{
    internal sealed class InstallationIdStore
    {
        private readonly string _path;

        public InstallationIdStore(string path)
        {
            _path = path ?? throw new ArgumentNullException(nameof(path));
        }

        public string TryLoadOrCreate()
        {
            try
            {
                if (File.Exists(_path))
                {
                    var existing = File.ReadAllText(_path, Encoding.UTF8).Trim();
                    if (TryNormalizeUuidV4(existing, out var canonical)) return canonical;
                }

                var created = Guid.NewGuid();
                var temporaryPath = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                var directory = Path.GetDirectoryName(_path);
                if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
                try
                {
                    File.WriteAllText(temporaryPath, created.ToString("D") + Environment.NewLine, new UTF8Encoding(false));
                    File.Move(temporaryPath, _path, true);
                    return created.ToString("D");
                }
                finally
                {
                    TryDelete(temporaryPath);
                }
            }
            catch
            {
                return null;
            }
        }

        internal static bool TryNormalizeUuidV4(string value, out string canonical)
        {
            canonical = null;
            if (!Guid.TryParseExact(value, "D", out var parsed)) return false;
            var normalized = parsed.ToString("D");
            var variant = normalized[19];
            if (normalized[14] != '4' ||
                variant != '8' && variant != '9' && variant != 'a' && variant != 'b') return false;
            canonical = normalized;
            return true;
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch
            {
                // A stale temporary file does not affect telemetry correctness.
            }
        }
    }
}
