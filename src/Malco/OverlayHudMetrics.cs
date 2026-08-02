using System;
using System.IO;
using System.Threading;
using Malco.Data;
using Malco.Diagnostics;
using Malco.Game.Services;
using Malco.Presentation.Scheduling;

namespace Malco
{
    internal sealed partial class OverlayHudMetrics : IDisposable
    {
        private const int MaxSamples = 600;
        private static readonly TimeSpan WriteInterval = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan CallbackGapThreshold = TimeSpan.FromMilliseconds(20);

        private readonly bool _enabled;
        private readonly object _sync;
        private readonly MetricRingBuffer _tickDurationsUs;
        private readonly MetricRingBuffer _renderDurationsUs;
        private readonly MetricRingBuffer _tickAllocatedBytes;
        private readonly MetricRingBuffer _renderAllocatedBytes;
        private readonly Timer _writeTimer;
        private readonly string _outputPath;
        private readonly string _scenario;
        private readonly DateTime _startedAtUtc;
        private readonly IProviderOptimizationMetricsSource _providerMetrics;
        private readonly CompositionFramePump _framePump;
        private long _lastRenderingTimestamp;
        private int _writeActive;
        private int _disposed;
        private long _tickCount;
        private long _renderCount;
        private long _activeRenderCount;
        private long _spatialSemanticRebuilds;
        private long _renderCallbackGapsOverThreshold;
        private long _activeRenderCallbackGapsOverThreshold;
        private long _managedAllocatedBytes;
        private long _renderManagedAllocatedBytes;
        private long _gen0Collections;
        private long _renderGen0Collections;
        private long _reportedWpfPositionWrites;
        private long _reportedVisualCreates;
        private long _reportedVisualUpdates;
        private long _reportedVisualRemoves;
        private CoordinatorMetricsSnapshot _coordinator;
        private DateTime _previousProcessSampleUtc;
        private TimeSpan _previousProcessCpu;
        private long _previousProcessTickCount;

        private OverlayHudMetrics(
            bool enabled,
            IProviderOptimizationMetricsSource providerMetrics,
            CompositionFramePump framePump)
        {
            _enabled = enabled;
            _providerMetrics = providerMetrics;
            _framePump = framePump;
            if (!enabled)
            {
                return;
            }

            _sync = new object();
            _tickDurationsUs = new MetricRingBuffer(MaxSamples);
            _renderDurationsUs = new MetricRingBuffer(MaxSamples);
            _tickAllocatedBytes = new MetricRingBuffer(MaxSamples);
            _renderAllocatedBytes = new MetricRingBuffer(MaxSamples);
            _scenario = Environment.GetEnvironmentVariable("MALCO_DIAGNOSTICS_SCENARIO") ?? string.Empty;
            _startedAtUtc = DateTime.UtcNow;
            Directory.CreateDirectory(AppPaths.UserDataDirectory);
            _outputPath = Path.Combine(AppPaths.UserDataDirectory, "overlay-hud-metrics.json");
            _writeTimer = new Timer(WriteSnapshot, null, WriteInterval, WriteInterval);
        }

        public static OverlayHudMetrics CreateFromEnvironment(
            IProviderOptimizationMetricsSource providerMetrics,
            CompositionFramePump framePump)
        {
            try
            {
                var enabled = string.Equals(
                    Environment.GetEnvironmentVariable("MALCO_DIAGNOSTICS"),
                    "1",
                    StringComparison.Ordinal);
                return new OverlayHudMetrics(enabled, providerMetrics, framePump);
            }
            catch
            {
                // Opt-in diagnostics must never prevent the overlay from starting.
                return new OverlayHudMetrics(false, providerMetrics, framePump);
            }
        }

        public bool Enabled => _enabled;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            _writeTimer?.Dispose();
        }

        private void WriteSnapshot(object state)
        {
            if (!_enabled || Volatile.Read(ref _disposed) != 0 ||
                Interlocked.Exchange(ref _writeActive, 1) != 0)
            {
                return;
            }

            try
            {
                MetricsSnapshot snapshot;
                lock (_sync)
                {
                    snapshot = CaptureSnapshot(DateTime.UtcNow);
                }

                File.WriteAllText(_outputPath, snapshot.BuildJson());
            }
            catch
            {
                // Diagnostics must never alter overlay behavior.
            }
            finally
            {
                Volatile.Write(ref _writeActive, 0);
            }
        }
    }
}
