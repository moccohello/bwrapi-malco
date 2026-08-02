using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Malco.Telemetry
{
    public sealed class TelemetryClient : IDisposable
    {
        private readonly TelemetryPolicy _policy;
        private readonly AtomicTelemetryQueue _queue;
        private readonly InstallationIdStore _installationId;
        private readonly TelemetryTransport _transport;
        private readonly ConcurrentQueue<TelemetryEvent> _incoming = new ConcurrentQueue<TelemetryEvent>();
        private readonly AutoResetEvent _wake = new AutoResetEvent(false);
        private readonly CancellationTokenSource _stopping = new CancellationTokenSource();
        private readonly Task _worker;
        private readonly string _clientVersion;
        private int _incomingCount;
        private int _disposed;

        private TelemetryClient(TelemetryPolicy policy, string dataDirectory, string clientVersion)
        {
            _policy = policy;
            _clientVersion = clientVersion ?? string.Empty;
            _queue = new AtomicTelemetryQueue(Path.Combine(dataDirectory, policy.QueueFileName), policy);
            _installationId = new InstallationIdStore(Path.Combine(dataDirectory, policy.InstallationIdFileName));
            _transport = new TelemetryTransport(policy);
            _worker = Task.Factory.StartNew(WorkerLoop, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();
        }

        public static TelemetryClient TryCreate(string policyPath, string dataDirectory, string clientVersion)
        {
            try
            {
                return new TelemetryClient(TelemetryPolicy.Load(policyPath), dataDirectory, clientVersion);
            }
            catch
            {
                return null;
            }
        }

        private void Track(TelemetryEvent item)
        {
            if (item == null || Volatile.Read(ref _disposed) != 0) return;
            var nextCount = Interlocked.Increment(ref _incomingCount);
            if (nextCount > _policy.MaxInMemoryEvents)
            {
                Interlocked.Decrement(ref _incomingCount);
                return;
            }
            _incoming.Enqueue(item);
            _wake.Set();
        }

        public void TrackGameStarted(string gameSessionId, string race, TelemetryGameSettings settings)
        {
            if (!Guid.TryParseExact(gameSessionId, "D", out var id)) return;
            var canonical = id.ToString("D");
            var variant = canonical[19];
            if (canonical[14] != '4' || variant != '8' && variant != '9' && variant != 'a' && variant != 'b') return;
            if ((race != "terran" && race != "zerg" && race != "protoss") || settings == null) return;
            Track(TelemetryEvent.Create("game_started", new Dictionary<string, object>
            {
                ["game_session_id"] = canonical,
                ["race"] = race,
                ["app_version"] = _clientVersion,
                ["settings"] = settings.ToProperties()
            }));
        }

        private async Task WorkerLoop()
        {
            var retrySeconds = _policy.RetryMinSeconds;
            var batchLimit = _policy.MaxBatchEvents;
            while (!_stopping.IsCancellationRequested)
            {
                try
                {
                    DrainIncoming();
                    var installId = _installationId.TryLoadOrCreate();
                    var batch = _queue.PeekBatch(batchLimit);
                    if (installId != null && batch.Count != 0)
                    {
                        var result = await _transport.SendBatchAsync(installId, batch, _stopping.Token).ConfigureAwait(false);
                        if (result == TelemetrySendResult.Accepted || result == TelemetrySendResult.PermanentFailure)
                        {
                            _queue.RemoveById(batch.Select(item => item.EventId));
                            retrySeconds = _policy.RetryMinSeconds;
                            batchLimit = _policy.MaxBatchEvents;
                            continue;
                        }
                        if (result == TelemetrySendResult.PayloadTooLarge)
                        {
                            if (batch.Count == 1)
                            {
                                _queue.RemoveById(batch.Select(item => item.EventId));
                                retrySeconds = _policy.RetryMinSeconds;
                                batchLimit = _policy.MaxBatchEvents;
                            }
                            else
                            {
                                batchLimit = Math.Max(1, batch.Count / 2);
                            }
                            continue;
                        }
                    }
                }
                catch (OperationCanceledException) when (_stopping.IsCancellationRequested)
                {
                    return;
                }
                catch
                {
                    // Network, serialization, and disk failures are telemetry-only.
                }
                if (_stopping.IsCancellationRequested) return;
                _wake.WaitOne(TimeSpan.FromSeconds(retrySeconds));
                retrySeconds = retrySeconds >= _policy.RetryMaxSeconds / 2
                    ? _policy.RetryMaxSeconds
                    : Math.Min(_policy.RetryMaxSeconds, retrySeconds * 2);
            }
        }

        private void DrainIncoming()
        {
            var items = new List<TelemetryEvent>();
            while (_incoming.TryDequeue(out var item))
            {
                Interlocked.Decrement(ref _incomingCount);
                items.Add(item);
            }
            if (items.Count != 0) _queue.Append(items);
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            _stopping.Cancel();
            _wake.Set();
            _transport.Dispose();
            _ = _worker.ContinueWith(
                _ =>
                {
                    _stopping.Dispose();
                    _wake.Dispose();
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }

    public sealed class TelemetryGameSettings
    {
        public TelemetryGameSettings(
            string language,
            int enabledFeatureCount,
            int visibleItemCount,
            int availableAlertCount,
            int completionAlertCount,
            string completionDisplayMode,
            int completionCountdownSeconds,
            int abilityDisplayModeCount,
            bool showTransportCargo)
        {
            Language = language;
            EnabledFeatureCount = enabledFeatureCount;
            VisibleItemCount = visibleItemCount;
            AvailableAlertCount = availableAlertCount;
            CompletionAlertCount = completionAlertCount;
            CompletionDisplayMode = completionDisplayMode;
            CompletionCountdownSeconds = completionCountdownSeconds;
            AbilityDisplayModeCount = abilityDisplayModeCount;
            ShowTransportCargo = showTransportCargo;
        }

        internal Dictionary<string, object> ToProperties()
        {
            return new Dictionary<string, object>
            {
                ["language"] = Language,
                ["enabled_feature_count"] = EnabledFeatureCount,
                ["visible_item_count"] = VisibleItemCount,
                ["available_alert_count"] = AvailableAlertCount,
                ["completion_alert_count"] = CompletionAlertCount,
                ["completion_display_mode"] = CompletionDisplayMode,
                ["completion_countdown_seconds"] = CompletionCountdownSeconds,
                ["ability_display_mode_count"] = AbilityDisplayModeCount,
                ["show_transport_cargo"] = ShowTransportCargo
            };
        }

        public string Language { get; }
        public int EnabledFeatureCount { get; }
        public int VisibleItemCount { get; }
        public int AvailableAlertCount { get; }
        public int CompletionAlertCount { get; }
        public string CompletionDisplayMode { get; }
        public int CompletionCountdownSeconds { get; }
        public int AbilityDisplayModeCount { get; }
        public bool ShowTransportCargo { get; }
    }
}
