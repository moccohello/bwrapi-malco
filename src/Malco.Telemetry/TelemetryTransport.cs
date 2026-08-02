using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Malco.Telemetry
{
    internal sealed class TelemetryTransport : IDisposable
    {
        private readonly TelemetryPolicy _policy;
        private readonly HttpClient _http;

        public TelemetryTransport(TelemetryPolicy policy)
        {
            _policy = policy ?? throw new ArgumentNullException(nameof(policy));
            _http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
            {
                Timeout = TimeSpan.FromSeconds(policy.RequestTimeoutSeconds)
            };
        }

        public async Task<TelemetrySendResult> SendBatchAsync(
            string installId,
            IReadOnlyList<TelemetryEvent> events,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(installId) || events == null || events.Count == 0)
                return TelemetrySendResult.Accepted;

            var body = JsonSerializer.SerializeToUtf8Bytes(new TelemetryBatch
            {
                SchemaVersion = 1,
                InstallId = installId,
                Events = events
            });
            using var request = CreateJsonPost(_policy.EventBatchUri, body);
            using var response = await _http.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.Accepted)
                return TelemetrySendResult.Accepted;
            if (response.StatusCode == HttpStatusCode.RequestEntityTooLarge)
                return TelemetrySendResult.PayloadTooLarge;
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
                return TelemetrySendResult.Retry;
            if ((int)response.StatusCode >= 400 && (int)response.StatusCode < 500)
                return TelemetrySendResult.PermanentFailure;
            return TelemetrySendResult.Retry;
        }

        private static HttpRequestMessage CreateJsonPost(Uri uri, byte[] body)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, uri)
            {
                Content = new ByteArrayContent(body)
            };
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json")
            {
                CharSet = "utf-8"
            };
            return request;
        }

        public void Dispose() => _http.Dispose();
    }

    internal enum TelemetrySendResult
    {
        Accepted,
        PermanentFailure,
        PayloadTooLarge,
        Retry
    }
}
