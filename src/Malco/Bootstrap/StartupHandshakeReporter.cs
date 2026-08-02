using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Threading;

namespace Malco.Bootstrap
{
    internal sealed class StartupHandshakeReporter
    {
        private const string MarkerSchema = "malco.startup-marker.v1";
        private static readonly Regex TokenPattern = new Regex(
            "^[0-9a-f]{64}$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private readonly string _activationId;
        private readonly string _markerDirectory;
        private Action _readyCallback;
        private bool _attached;

        private StartupHandshakeReporter(string activationId, string markerDirectory)
        {
            _activationId = activationId;
            _markerDirectory = markerDirectory;
        }

        public static bool TryCreate(string activationId, out StartupHandshakeReporter reporter)
        {
            reporter = null;
            if (string.IsNullOrEmpty(activationId) ||
                !TokenPattern.IsMatch(activationId))
            {
                return false;
            }

            try
            {
                var payloadDirectory = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar));
                var versionDirectory = payloadDirectory.Parent;
                var versionsDirectory = versionDirectory?.Parent;
                var installDirectory = versionsDirectory?.Parent;
                if (versionDirectory == null || versionsDirectory == null || installDirectory == null ||
                    !string.Equals(payloadDirectory.Name, "payload", StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(versionsDirectory.Name, "versions", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                var markerDirectory = Path.GetFullPath(Path.Combine(
                    installDirectory.FullName,
                    "state",
                    "startup"));
                var stateRoot = Path.GetFullPath(Path.Combine(installDirectory.FullName, "state"))
                    .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (!markerDirectory.StartsWith(stateRoot, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                reporter = new StartupHandshakeReporter(activationId, markerDirectory);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public void Attach(Window window, Action readyCallback = null)
        {
            if (window == null) throw new ArgumentNullException(nameof(window));
            if (_attached) throw new InvalidOperationException("The startup reporter is already attached.");
            _attached = true;
            _readyCallback = readyCallback;

            EventHandler initialized = null;
            initialized = (sender, args) =>
            {
                window.SourceInitialized -= initialized;
                window.Dispatcher.BeginInvoke(
                    DispatcherPriority.ApplicationIdle,
                    new Action(WriteMarker));
            };
            window.SourceInitialized += initialized;
        }

        private void WriteMarker()
        {
            try
            {
                Directory.CreateDirectory(_markerDirectory);
                var markerPath = Path.Combine(_markerDirectory, _activationId + ".json");
                var temporaryPath = markerPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
                var marker = new StartupMarker
                {
                    Schema = MarkerSchema,
                    ActivationId = _activationId,
                    ProcessId = Process.GetCurrentProcess().Id
                };
                var bytes = JsonSerializer.SerializeToUtf8Bytes(marker);

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
                    File.Move(temporaryPath, markerPath, false);
                    try { _readyCallback?.Invoke(); }
                    catch { }
                }
                finally
                {
                    if (File.Exists(temporaryPath))
                    {
                        File.Delete(temporaryPath);
                    }
                }
            }
            catch (IOException)
            {
                // Failure to publish readiness is fail-closed at the launcher:
                // its bounded wait will reject this activation.
            }
            catch (UnauthorizedAccessException)
            {
                // See the fail-closed launcher behavior above.
            }
        }

        private sealed class StartupMarker
        {
            [JsonPropertyName("schema")]
            public string Schema { get; set; }

            [JsonPropertyName("activation_id")]
            public string ActivationId { get; set; }

            [JsonPropertyName("process_id")]
            public int ProcessId { get; set; }
        }
    }
}
