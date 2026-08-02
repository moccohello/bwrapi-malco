using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Malco.Telemetry
{
    internal sealed class TelemetryEvent
    {
        [JsonPropertyName("event_id")]
        public string EventId { get; set; }

        [JsonPropertyName("event_name")]
        public string EventName { get; set; }

        [JsonPropertyName("occurred_at_utc")]
        public string OccurredAtUtc { get; set; }

        [JsonPropertyName("properties")]
        public IReadOnlyDictionary<string, object> Properties { get; set; }

        internal static TelemetryEvent Create(string eventName, IReadOnlyDictionary<string, object> properties = null)
        {
            if (string.IsNullOrWhiteSpace(eventName)) throw new ArgumentException("An event name is required.", nameof(eventName));
            return new TelemetryEvent
            {
                EventId = Guid.NewGuid().ToString("D"),
                EventName = eventName,
                OccurredAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                Properties = properties ?? new Dictionary<string, object>()
            };
        }
    }

    internal sealed class TelemetryBatch
    {
        [JsonPropertyName("schema_version")]
        public int SchemaVersion { get; set; }

        [JsonPropertyName("install_id")]
        public string InstallId { get; set; }

        [JsonPropertyName("events")]
        public IReadOnlyList<TelemetryEvent> Events { get; set; }
    }
}
