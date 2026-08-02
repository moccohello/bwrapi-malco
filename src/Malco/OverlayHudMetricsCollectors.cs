using System;
using System.Diagnostics;
using Malco.Diagnostics;
using Malco.Game.Services;

namespace Malco
{
    internal readonly struct OverlayHudProbe
    {
        public OverlayHudProbe(long startedTimestamp, long allocatedBytesBefore, int gen0CollectionsBefore)
        {
            Enabled = true;
            StartedTimestamp = startedTimestamp;
            AllocatedBytesBefore = allocatedBytesBefore;
            Gen0CollectionsBefore = gen0CollectionsBefore;
        }

        public bool Enabled { get; }
        public long StartedTimestamp { get; }
        public long AllocatedBytesBefore { get; }
        public int Gen0CollectionsBefore { get; }
    }

    internal sealed partial class OverlayHudMetrics
    {
        public OverlayHudProbe BeginProbe()
        {
            if (!_enabled)
            {
                return default;
            }

            return new OverlayHudProbe(
                Stopwatch.GetTimestamp(),
                GC.GetAllocatedBytesForCurrentThread(),
                GC.CollectionCount(0));
        }

        public void CompleteTick(
            OverlayHudProbe probe,
            bool spatialSemanticRebuilt,
            CoordinatorMetricsSnapshot coordinator)
        {
            if (!_enabled || !probe.Enabled)
            {
                return;
            }

            var elapsed = Stopwatch.GetElapsedTime(probe.StartedTimestamp);
            var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - probe.AllocatedBytesBefore;
            var gen0Collections = GC.CollectionCount(0) - probe.Gen0CollectionsBefore;
            lock (_sync)
            {
                _tickCount++;
                AddSample(_tickDurationsUs, elapsed);
                if (spatialSemanticRebuilt)
                {
                    _spatialSemanticRebuilds++;
                }

                long nonnegativeAllocatedBytes = Math.Max(0, allocatedBytes);
                _managedAllocatedBytes += nonnegativeAllocatedBytes;
                _tickAllocatedBytes.Add(nonnegativeAllocatedBytes);
                _gen0Collections += Math.Max(0, gen0Collections);
                _coordinator = coordinator;
            }
        }

        public void RecordSpatialMutations(int creates, int updates, int removes)
        {
            if (!_enabled)
            {
                return;
            }

            lock (_sync)
            {
                _reportedVisualCreates += Math.Max(0, creates);
                _reportedVisualUpdates += Math.Max(0, updates);
                _reportedVisualRemoves += Math.Max(0, removes);
            }
        }

        public void RecordPositionWrites(int writes)
        {
            if (!_enabled)
            {
                return;
            }

            lock (_sync)
            {
                _reportedWpfPositionWrites += Math.Max(0, writes);
            }
        }

        public void CompleteRendering(OverlayHudProbe probe, bool activeSpatialPresentation)
        {
            if (!_enabled || !probe.Enabled)
            {
                return;
            }

            var completedTimestamp = Stopwatch.GetTimestamp();
            var elapsed = Stopwatch.GetElapsedTime(probe.StartedTimestamp, completedTimestamp);
            var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - probe.AllocatedBytesBefore;
            var gen0Collections = GC.CollectionCount(0) - probe.Gen0CollectionsBefore;
            lock (_sync)
            {
                _renderCount++;
                AddSample(_renderDurationsUs, elapsed);
                long nonnegativeAllocatedBytes = Math.Max(0, allocatedBytes);
                _renderManagedAllocatedBytes += nonnegativeAllocatedBytes;
                _renderAllocatedBytes.Add(nonnegativeAllocatedBytes);
                _renderGen0Collections += Math.Max(0, gen0Collections);
                if (activeSpatialPresentation)
                {
                    _activeRenderCount++;
                }

                if (_lastRenderingTimestamp != 0 &&
                    Stopwatch.GetElapsedTime(_lastRenderingTimestamp, completedTimestamp) > CallbackGapThreshold)
                {
                    _renderCallbackGapsOverThreshold++;
                    if (activeSpatialPresentation)
                    {
                        _activeRenderCallbackGapsOverThreshold++;
                    }
                }

                _lastRenderingTimestamp = completedTimestamp;
            }
        }

        private static void AddSample(MetricRingBuffer samples, TimeSpan elapsed)
        {
            samples.Add(Math.Max(0, (long)(elapsed.TotalMilliseconds * 1000d)));
        }
    }
}
