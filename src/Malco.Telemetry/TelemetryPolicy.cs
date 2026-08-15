using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Malco.Telemetry
{
    public sealed class TelemetryPolicy
    {
        public int SchemaVersion { get; set; }
        public string ServiceBaseUri { get; set; }
        public string EventBatchPath { get; set; }
        public string InstallationIdFileName { get; set; }
        public string QueueFileName { get; set; }
        public string QueueMutexName { get; set; }
        public int MaxPendingEvents { get; set; }
        public int MaxQueueBytes { get; set; }
        public int MaxEventBytes { get; set; }
        public int MaxBatchEvents { get; set; }
        public int MaxInMemoryEvents { get; set; }
        public int RequestTimeoutSeconds { get; set; }
        public int RetryMinSeconds { get; set; }
        public int RetryMaxSeconds { get; set; }
        public int MutexWaitMilliseconds { get; set; }

        public Uri EventBatchUri => new Uri(new Uri(ServiceBaseUri, UriKind.Absolute), EventBatchPath);

        public static TelemetryPolicy Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A telemetry policy path is required.", nameof(path));
            var policy = JsonSerializer.Deserialize<TelemetryPolicy>(
                File.ReadAllText(path),
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
                });
            if (policy == null) throw new InvalidDataException("The telemetry policy is empty.");
            policy.Validate();
            return policy;
        }

        private void Validate()
        {
            if (SchemaVersion != 1) throw new InvalidDataException("Unsupported telemetry policy schema.");
            if (!Uri.TryCreate(ServiceBaseUri, UriKind.Absolute, out var serviceUri) ||
                serviceUri.Scheme != Uri.UriSchemeHttps ||
                !string.Equals(serviceUri.Host, "api.horangmalco.com", StringComparison.OrdinalIgnoreCase) ||
                !serviceUri.IsDefaultPort ||
                !string.IsNullOrEmpty(serviceUri.UserInfo) ||
                serviceUri.AbsolutePath != "/" ||
                !string.IsNullOrEmpty(serviceUri.Query) ||
                !string.IsNullOrEmpty(serviceUri.Fragment))
                throw new InvalidDataException("Telemetry service_base_uri must be the production HTTPS API origin.");
            if (!string.Equals(EventBatchPath, "/api/v1/events/batch", StringComparison.Ordinal))
                throw new InvalidDataException("EventBatchPath does not match the production API contract.");
            RequireFileName(InstallationIdFileName, nameof(InstallationIdFileName));
            RequireFileName(QueueFileName, nameof(QueueFileName));
            if (string.IsNullOrWhiteSpace(QueueMutexName)) throw new InvalidDataException("queue_mutex_name is required.");
            RequirePositive(MaxPendingEvents, nameof(MaxPendingEvents));
            RequirePositive(MaxQueueBytes, nameof(MaxQueueBytes));
            RequirePositive(MaxEventBytes, nameof(MaxEventBytes));
            RequirePositive(MaxBatchEvents, nameof(MaxBatchEvents));
            RequirePositive(MaxInMemoryEvents, nameof(MaxInMemoryEvents));
            RequirePositive(RequestTimeoutSeconds, nameof(RequestTimeoutSeconds));
            RequirePositive(RetryMinSeconds, nameof(RetryMinSeconds));
            RequirePositive(RetryMaxSeconds, nameof(RetryMaxSeconds));
            RequirePositive(MutexWaitMilliseconds, nameof(MutexWaitMilliseconds));
            if (MaxBatchEvents > 32) throw new InvalidDataException("max_batch_events exceeds the server contract.");
            if (RetryMaxSeconds < RetryMinSeconds) throw new InvalidDataException("retry_max_seconds must be at least retry_min_seconds.");
            if (MaxBatchEvents > MaxPendingEvents) throw new InvalidDataException("max_batch_events cannot exceed max_pending_events.");
            if (MaxEventBytes > MaxQueueBytes) throw new InvalidDataException("max_event_bytes cannot exceed max_queue_bytes.");
            if (MaxQueueBytes > 67108864) throw new InvalidDataException("max_queue_bytes exceeds the safety ceiling.");
            if (RequestTimeoutSeconds > 300) throw new InvalidDataException("request_timeout_seconds exceeds the safety ceiling.");
            if (RetryMaxSeconds > 86400) throw new InvalidDataException("Telemetry wait interval exceeds the safety ceiling.");
            if (MutexWaitMilliseconds > 60000) throw new InvalidDataException("mutex_wait_milliseconds exceeds the safety ceiling.");
        }

        private static void RequireFileName(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value) || !string.Equals(Path.GetFileName(value), value, StringComparison.Ordinal))
                throw new InvalidDataException(name + " must be a file name.");
        }

        private static void RequirePositive(int value, string name)
        {
            if (value <= 0) throw new InvalidDataException(name + " must be positive.");
        }
    }
}
