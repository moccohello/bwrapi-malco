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
                    if (IsUuidV4(existing, out var id)) return id.ToString("D");
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

        private static bool IsUuidV4(string value, out Guid id)
        {
            id = default;
            if (!Guid.TryParseExact(value, "D", out var parsed)) return false;
            var canonical = parsed.ToString("D");
            var variant = canonical[19];
            if (canonical[14] != '4' || variant != '8' && variant != '9' && variant != 'a' && variant != 'b') return false;
            id = parsed;
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
