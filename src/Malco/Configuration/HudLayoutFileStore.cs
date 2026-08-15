using System;
using System.IO;
using System.Text;
using System.Text.Json;
using Malco.Configuration.Models;

namespace Malco.Configuration
{
    internal sealed partial class HudLayoutFileStore
    {
        internal const int CurrentSchemaVersion = 3;

        private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        private readonly string _path;
        private bool _writeBlocked;
        private string _writeBlockedMessage = string.Empty;

        public bool IsWriteBlocked { get { return _writeBlocked; } }

        public HudLayoutFileStore(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("A layout path is required.", nameof(path));
            }

            _path = path;
        }

        public LayoutLoadResult Load()
        {
            _writeBlocked = false;
            _writeBlockedMessage = string.Empty;

            if (!File.Exists(_path))
            {
                return new LayoutLoadResult(
                    LayoutLoadStatus.Missing,
                    HudLayoutConfig.CreateDefault(),
                    false,
                    "No settings file exists; defaults are active.");
            }

            HudLayoutConfig primary;
            bool primaryNewer;
            bool primaryUnsupported;
            if (!TryRead(_path, out primary, out primaryNewer, out primaryUnsupported))
            {
                if (primaryNewer)
                {
                    return BlockedLoad(
                        LayoutLoadStatus.NewerSchemaPreserved,
                        "The settings file uses a newer schema and was left unchanged. " +
                        "Back up, move or rename hud-layout.json, then retry save.");
                }
                if (primaryUnsupported)
                {
                    return new LayoutLoadResult(
                        LayoutLoadStatus.ResetToDefaults,
                        HudLayoutConfig.CreateDefault(),
                        false,
                        "The previous settings schema was reset; defaults are active.");
                }
                return BlockedLoad(
                    LayoutLoadStatus.CorruptSourcePreserved,
                    "The settings file could not be read and was left unchanged. " +
                    "Back up, move or rename hud-layout.json, then retry save.");
            }

            return new LayoutLoadResult(
                LayoutLoadStatus.Loaded,
                primary,
                false,
                "Settings loaded.");
        }

        public LayoutSaveResult Save(HudLayoutSnapshot snapshot)
        {
            if (_writeBlocked)
            {
                return new LayoutSaveResult(LayoutSaveStatus.WriteBlocked, _writeBlockedMessage);
            }
            string attemptPath = null;
            try
            {
                attemptPath = WriteNewTemporary(snapshot);
                File.Move(attemptPath, _path, true);
                attemptPath = null;

                return new LayoutSaveResult(LayoutSaveStatus.Saved, "Settings saved.");
            }
            catch (Exception ex)
            {
                DeleteAttemptFile(attemptPath);
                return new LayoutSaveResult(
                    LayoutSaveStatus.Failed,
                    "Settings could not be saved. The previous source was left unchanged: " +
                    ex.Message);
            }
        }

        public LayoutSaveResult RecoverAndSave(HudLayoutSnapshot snapshot)
        {
            if (!_writeBlocked)
            {
                return Save(snapshot);
            }
            if (File.Exists(_path))
            {
                return new LayoutSaveResult(LayoutSaveStatus.WriteBlocked, _writeBlockedMessage);
            }

            string attemptPath = null;
            try
            {
                attemptPath = WriteNewTemporary(snapshot);
                // File.Move does not overwrite. If another process recreates the
                // preserved primary after the checks above, recovery fails closed.
                File.Move(attemptPath, _path);
                attemptPath = null;
                _writeBlocked = false;
                _writeBlockedMessage = string.Empty;
                return new LayoutSaveResult(LayoutSaveStatus.Saved, "Settings recovered and saved.");
            }
            catch (Exception ex)
            {
                DeleteAttemptFile(attemptPath);
                return new LayoutSaveResult(
                    LayoutSaveStatus.Failed,
                    "Settings recovery could not create a new source: " +
                    ex.Message);
            }
        }

        private static void DeleteAttemptFile(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            try
            {
                File.Delete(path);
            }
            catch
            {
                // The original settings file remains authoritative.
            }
        }

        private string WriteNewTemporary(HudLayoutSnapshot snapshot)
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            var document = (snapshot ?? HudLayoutSnapshot.FromLayout(null)).ToMutable();
            document.SchemaVersion = CurrentSchemaVersion;
            var json = JsonSerializer.Serialize(document, SerializerOptions);
            var attemptPath = _path + ".saving." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(
                    attemptPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None))
                {
                    using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                    {
                        writer.Write(json);
                    }
                }
            }
            catch
            {
                DeleteAttemptFile(attemptPath);
                throw;
            }
            return attemptPath;
        }

        private LayoutLoadResult BlockedLoad(
            LayoutLoadStatus status,
            string message,
            HudLayoutConfig safeLayout = null)
        {
            _writeBlocked = true;
            _writeBlockedMessage = message ?? "Settings writes are blocked until recovery is acknowledged.";
            return new LayoutLoadResult(status, safeLayout ?? HudLayoutConfig.CreateDefault(), true, _writeBlockedMessage);
        }
    }
}
