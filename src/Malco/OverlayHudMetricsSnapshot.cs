using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using Malco.Data;
using Malco.Diagnostics;
using Malco.Game.Services;
using Malco.Presentation.Scheduling;

namespace Malco
{
    internal sealed partial class OverlayHudMetrics
    {
        private MetricsSnapshot CaptureSnapshot(DateTime capturedAtUtc)
        {
            ProviderPerformanceCounters providerPerformance = _providerMetrics != null
                ? _providerMetrics.GetPerformanceCounters()
                : default;
            FramePumpCounters framePump = _framePump != null
                ? _framePump.GetCounters()
                : default;
            return new MetricsSnapshot
            {
                CapturedAtUtc = capturedAtUtc,
                StartedAtUtc = _startedAtUtc,
                Scenario = _scenario,
                TickCount = _tickCount,
                RenderCount = _renderCount,
                ActiveRenderCount = _activeRenderCount,
                TickDurationsUs = _tickDurationsUs.ToArray(),
                RenderDurationsUs = _renderDurationsUs.ToArray(),
                TickAllocatedBytes = _tickAllocatedBytes.ToArray(),
                RenderAllocatedBytes = _renderAllocatedBytes.ToArray(),
                RenderCallbackGapsOverThreshold = _renderCallbackGapsOverThreshold,
                ActiveRenderCallbackGapsOverThreshold = _activeRenderCallbackGapsOverThreshold,
                SpatialSemanticRebuilds = _spatialSemanticRebuilds,
                ManagedAllocatedBytes = _managedAllocatedBytes,
                RenderManagedAllocatedBytes = _renderManagedAllocatedBytes,
                Gen0Collections = _gen0Collections,
                RenderGen0Collections = _renderGen0Collections,
                ReportedWpfPositionWrites = _reportedWpfPositionWrites,
                ReportedVisualCreates = _reportedVisualCreates,
                ReportedVisualUpdates = _reportedVisualUpdates,
                ReportedVisualRemoves = _reportedVisualRemoves,
                Coordinator = _coordinator,
                Provider = _providerMetrics != null
                    ? _providerMetrics.GetOptimizationCounters()
                    : default,
                ProviderPerformance = providerPerformance,
                FramePump = framePump,
                Process = CaptureProcessMetrics(capturedAtUtc)
            };
        }

        private ProcessMetricsSnapshot CaptureProcessMetrics(DateTime capturedAtUtc)
        {
            try
            {
                using Process process = Process.GetCurrentProcess();
                process.Refresh();
                TimeSpan totalCpu = process.TotalProcessorTime;
                double? normalizedCpuPercent = null;
                double? cpuMicrosecondsPerTick = null;
                if (_previousProcessSampleUtc != default)
                {
                    double elapsedSeconds = (capturedAtUtc - _previousProcessSampleUtc).TotalSeconds;
                    double cpuSeconds = Math.Max(0, (totalCpu - _previousProcessCpu).TotalSeconds);
                    long intervalTicks = Math.Max(0, _tickCount - _previousProcessTickCount);
                    if (elapsedSeconds > 0)
                    {
                        normalizedCpuPercent = cpuSeconds * 100d /
                            (elapsedSeconds * Math.Max(1, Environment.ProcessorCount));
                    }
                    if (intervalTicks > 0)
                        cpuMicrosecondsPerTick = cpuSeconds * 1000000d / intervalTicks;
                }

                _previousProcessSampleUtc = capturedAtUtc;
                _previousProcessCpu = totalCpu;
                _previousProcessTickCount = _tickCount;
                return new ProcessMetricsSnapshot(
                    totalCpu.TotalMilliseconds,
                    normalizedCpuPercent,
                    cpuMicrosecondsPerTick,
                    process.WorkingSet64,
                    process.PrivateMemorySize64,
                    Environment.ProcessorCount);
            }
            catch
            {
                return default;
            }
        }

        private readonly struct ProcessMetricsSnapshot
        {
            public ProcessMetricsSnapshot(
                double totalCpuMilliseconds,
                double? normalizedCpuPercent,
                double? cpuMicrosecondsPerTick,
                long workingSetBytes,
                long privateMemoryBytes,
                int processorCount)
            {
                TotalCpuMilliseconds = totalCpuMilliseconds;
                NormalizedCpuPercent = normalizedCpuPercent;
                CpuMicrosecondsPerTick = cpuMicrosecondsPerTick;
                WorkingSetBytes = workingSetBytes;
                PrivateMemoryBytes = privateMemoryBytes;
                ProcessorCount = processorCount;
            }

            public double TotalCpuMilliseconds { get; }
            public double? NormalizedCpuPercent { get; }
            public double? CpuMicrosecondsPerTick { get; }
            public long WorkingSetBytes { get; }
            public long PrivateMemoryBytes { get; }
            public int ProcessorCount { get; }
        }

        private sealed class MetricsSnapshot
        {
            public DateTime CapturedAtUtc { get; set; }
            public DateTime StartedAtUtc { get; set; }
            public string Scenario { get; set; }
            public long TickCount { get; set; }
            public long RenderCount { get; set; }
            public long ActiveRenderCount { get; set; }
            public long[] TickDurationsUs { get; set; }
            public long[] RenderDurationsUs { get; set; }
            public long[] TickAllocatedBytes { get; set; }
            public long[] RenderAllocatedBytes { get; set; }
            public long RenderCallbackGapsOverThreshold { get; set; }
            public long ActiveRenderCallbackGapsOverThreshold { get; set; }
            public long SpatialSemanticRebuilds { get; set; }
            public long ManagedAllocatedBytes { get; set; }
            public long RenderManagedAllocatedBytes { get; set; }
            public long Gen0Collections { get; set; }
            public long RenderGen0Collections { get; set; }
            public long ReportedWpfPositionWrites { get; set; }
            public long ReportedVisualCreates { get; set; }
            public long ReportedVisualUpdates { get; set; }
            public long ReportedVisualRemoves { get; set; }
            public CoordinatorMetricsSnapshot Coordinator { get; set; }
            public ProviderOptimizationCounters Provider { get; set; }
            public ProviderPerformanceCounters ProviderPerformance { get; set; }
            public FramePumpCounters FramePump { get; set; }
            public ProcessMetricsSnapshot Process { get; set; }

            public string BuildJson()
            {
                var elapsedSeconds = Math.Max(0.001d, (CapturedAtUtc - StartedAtUtc).TotalSeconds);
                var payload = new
                {
                    schema = "bwrapi.overlay_hud_metrics.v4",
                    scenario = Scenario,
                    started_at_utc = StartedAtUtc,
                    captured_at_utc = CapturedAtUtc,
                    elapsed_seconds = elapsedSeconds,
                    tick_count = TickCount,
                    tick_rate_hz = TickCount / elapsedSeconds,
                    render_callback_count = RenderCount,
                    render_callback_rate_hz = RenderCount / elapsedSeconds,
                    active_render_callback_count = ActiveRenderCount,
                    on_tick_p50_us = Percentile(TickDurationsUs, 50),
                    on_tick_p95_us = Percentile(TickDurationsUs, 95),
                    on_tick_p99_us = Percentile(TickDurationsUs, 99),
                    on_rendering_p50_us = Percentile(RenderDurationsUs, 50),
                    on_rendering_p95_us = Percentile(RenderDurationsUs, 95),
                    on_rendering_p99_us = Percentile(RenderDurationsUs, 99),
                    render_callback_gaps_over_20ms = RenderCallbackGapsOverThreshold,
                    active_render_callback_gaps_over_20ms = ActiveRenderCallbackGapsOverThreshold,
                    spatial_semantic_rebuilds = SpatialSemanticRebuilds,
                    managed_allocated_bytes = ManagedAllocatedBytes,
                    managed_allocated_bytes_per_second = ManagedAllocatedBytes / elapsedSeconds,
                    rendering_managed_allocated_bytes = RenderManagedAllocatedBytes,
                    rendering_managed_allocated_bytes_per_second = RenderManagedAllocatedBytes / elapsedSeconds,
                    gen0_collections = Gen0Collections,
                    rendering_gen0_collections = RenderGen0Collections,
                    reported_wpf_position_writes = ReportedWpfPositionWrites,
                    reported_visual_create_count = ReportedVisualCreates,
                    reported_visual_update_count = ReportedVisualUpdates,
                    reported_visual_remove_count = ReportedVisualRemoves,
                    semantic_publications = Coordinator.SemanticPublications,
                    command_publications = Coordinator.CommandPublications,
                    viewport_publications = Coordinator.ViewportPublications,
                    envelope_publications = Coordinator.EnvelopePublications,
                    reducer_no_ops = Coordinator.ReducerNoOps,
                    provider_semantic_polls = Provider.SemanticPolls,
                    provider_semantic_poll_rate_hz = Provider.SemanticPolls / elapsedSeconds,
                    provider_semantic_conversions = Provider.SemanticConversions,
                    provider_semantic_commits = Provider.SemanticCommits,
                    provider_command_polls = Provider.CommandPolls,
                    provider_command_poll_rate_hz = Provider.CommandPolls / elapsedSeconds,
                    provider_command_conversions = Provider.CommandConversions,
                    provider_command_commits = Provider.CommandCommits,
                    provider_projection_polls = Provider.ProjectionPolls,
                    provider_projection_poll_rate_hz = Provider.ProjectionPolls / elapsedSeconds,
                    provider_projection_commits = Provider.ProjectionCommits,
                    frame_pump_projection_commits = FramePump.ProjectionCommits,
                    frame_pump_dispatcher_drains = FramePump.DispatcherDrains,
                    frame_pump_coalesced_skips = FramePump.CoalescedSkips,
                    frame_pump_last_commit_to_apply_us = FramePump.LastCommitToApplyTicks <= 0
                        ? 0d
                        : FramePump.LastCommitToApplyTicks * 1000000d / Stopwatch.Frequency,
                    rolling_distributions = new
                    {
                        hud_tick_duration_us = Distribution(TickDurationsUs),
                        hud_tick_allocated_bytes = Distribution(TickAllocatedBytes),
                        rendering_duration_us = Distribution(RenderDurationsUs),
                        rendering_allocated_bytes = Distribution(RenderAllocatedBytes),
                        provider_semantic = ChannelDistribution(ProviderPerformance.Semantic),
                        provider_viewport = ChannelDistribution(ProviderPerformance.Viewport),
                        provider_commands = ChannelDistribution(ProviderPerformance.Commands),
                        frame_pump_latest_commit_to_apply_us = Distribution(
                            TicksToMicroseconds(FramePump.ApplyLatencyTicks))
                    },
                    frame_pump_pending_frame = FramePump.PendingFrame,
                    process = new
                    {
                        cpu_total_ms = Process.TotalCpuMilliseconds,
                        cpu_normalized_percent_interval = Process.NormalizedCpuPercent,
                        cpu_us_per_hud_tick_interval = Process.CpuMicrosecondsPerTick,
                        working_set_bytes = Process.WorkingSetBytes,
                        private_memory_bytes = Process.PrivateMemoryBytes,
                        logical_processor_count = Process.ProcessorCount
                    }
                };
                return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
            }
        }

        private static object ChannelDistribution(PerformanceChannelSnapshot channel) => new
        {
            successful_call_duration_us = Distribution(channel.DurationMicroseconds),
            successful_call_allocated_bytes = Distribution(channel.AllocatedBytes)
        };

        private static object Distribution(long[] values)
        {
            long[] sorted = (values ?? Array.Empty<long>()).OrderBy(value => value).ToArray();
            return new
            {
                sample_count = sorted.Length,
                p50 = PercentileSorted(sorted, 50),
                p95 = PercentileSorted(sorted, 95),
                p99 = PercentileSorted(sorted, 99),
                maximum = sorted.Length == 0 ? 0 : sorted[sorted.Length - 1]
            };
        }

        private static long[] TicksToMicroseconds(long[] ticks)
        {
            if (ticks == null || ticks.Length == 0) return Array.Empty<long>();
            var result = new long[ticks.Length];
            for (var index = 0; index < ticks.Length; index++)
                result[index] = Math.Max(0, (long)(ticks[index] * 1000000d / Stopwatch.Frequency));
            return result;
        }

        private static long PercentileSorted(long[] sorted, int percentile)
        {
            if (sorted == null || sorted.Length == 0) return 0;
            var index = (int)Math.Ceiling(sorted.Length * percentile / 100d) - 1;
            return sorted[Math.Max(0, Math.Min(sorted.Length - 1, index))];
        }

        private static long Percentile(IEnumerable<long> values, int percentile)
        {
            var sorted = values.OrderBy(value => value).ToList();
            if (sorted.Count == 0)
            {
                return 0;
            }

            var index = (int)Math.Ceiling(sorted.Count * percentile / 100d) - 1;
            return sorted[Math.Max(0, Math.Min(sorted.Count - 1, index))];
        }
    }
}
