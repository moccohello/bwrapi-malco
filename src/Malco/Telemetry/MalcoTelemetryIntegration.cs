using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Malco.Application.Contracts.Output;
using Malco.Configuration.Models;
using Malco.Data;
using Malco.Models;
using Malco.Telemetry;

namespace Malco.Integration.Telemetry
{
    internal sealed class MalcoTelemetryIntegration : IOverlayStateCommitSink, IDisposable
    {
        private readonly TelemetryClient _client;
        private readonly Func<HudLayoutSnapshot> _settingsSnapshot;
        private readonly object _sessionSync = new object();
        private readonly HashSet<string> _observedSessionTuples = new HashSet<string>(StringComparer.Ordinal);
        private bool _disposed;

        public MalcoTelemetryIntegration(
            TelemetryClient client,
            Func<HudLayoutSnapshot> settingsSnapshot)
        {
            _client = client;
            _settingsSnapshot = settingsSnapshot;
        }

        public void MarkOverlayStateCommitted(OverlayReadModel state)
        {
            if (_client == null || _disposed) return;
            try
            {
                Observe(state?.Semantic);
            }
            catch
            {
                // Provider publication must never depend on telemetry.
            }
        }

        private void Observe(SemanticSnapshotState semantic)
        {
            if (semantic?.Snapshot == null || !semantic.Snapshot.IsInMatch) return;
            var race = NormalizeRace(semantic.Snapshot.Race);
            if (race == null) return;

            var sessionEpoch = semantic.SessionEpoch ?? string.Empty;
            var tuple = sessionEpoch.Length.ToString(CultureInfo.InvariantCulture) + ":" +
                        sessionEpoch + ":" +
                        semantic.SessionGeneration.ToString(CultureInfo.InvariantCulture);
            var settings = _settingsSnapshot?.Invoke();
            if (settings == null) return;
            var telemetrySettings = new TelemetryGameSettings(
                settings.Language,
                settings.Widgets.Count(entry => entry.Value.Enabled),
                settings.ItemSettings.Count(entry => entry.Value.Show),
                settings.ItemSettings.Count(entry => entry.Value.AvailableAlert),
                settings.ItemSettings.Count(entry => entry.Value.CompletionAlert),
                settings.CompletionDisplayMode,
                settings.CompletionCountdownSeconds,
                settings.AbilityDisplayModes.Count,
                settings.ShowTransportCargo);
            lock (_sessionSync)
            {
                if (_observedSessionTuples.Contains(tuple)) return;
                _observedSessionTuples.Add(tuple);
            }

            try
            {
                _client.TrackGameStarted(
                    Guid.NewGuid().ToString("D"),
                    race,
                    telemetrySettings);
            }
            catch
            {
                lock (_sessionSync) _observedSessionTuples.Remove(tuple);
                throw;
            }
        }

        private static string NormalizeRace(Race race)
        {
            switch (race)
            {
                case Race.Terran: return "terran";
                case Race.Zerg: return "zerg";
                case Race.Protoss: return "protoss";
                default: return null;
            }
        }

        public void Dispose()
        {
            _disposed = true;
            try { _client?.Dispose(); }
            catch { }
        }
    }
}
