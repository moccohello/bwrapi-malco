using System;
using System.Diagnostics;

namespace Malco.Diagnostics
{
    internal static class DiagnosticsSwitches
    {
        public static bool PerformanceEnabled => string.Equals(
            Environment.GetEnvironmentVariable("MALCO_DIAGNOSTICS"),
            "1",
            StringComparison.Ordinal);
    }

    internal readonly struct PerformanceProbe
    {
        public PerformanceProbe(long startedTimestamp, long allocatedBytesBefore)
        {
            Enabled = true;
            StartedTimestamp = startedTimestamp;
            AllocatedBytesBefore = allocatedBytesBefore;
        }

        public bool Enabled { get; }
        public long StartedTimestamp { get; }
        public long AllocatedBytesBefore { get; }
    }

    internal readonly struct PerformanceChannelSnapshot
    {
        public PerformanceChannelSnapshot(long[] durationMicroseconds, long[] allocatedBytes)
        {
            DurationMicroseconds = durationMicroseconds ?? Array.Empty<long>();
            AllocatedBytes = allocatedBytes ?? Array.Empty<long>();
        }

        public long[] DurationMicroseconds { get; }
        public long[] AllocatedBytes { get; }
    }

    /// <summary>
    /// A fixed-capacity, opt-in sample channel. When diagnostics are disabled it
    /// allocates no buffers and Begin/Complete perform only a null check.
    /// </summary>
    internal sealed class OptInPerformanceChannel
    {
        private readonly ConcurrentPerformanceRing _samples;

        public OptInPerformanceChannel(bool enabled, int capacity)
        {
            if (!enabled) return;
            _samples = new ConcurrentPerformanceRing(capacity);
        }

        public PerformanceProbe Begin()
        {
            if (_samples == null) return default;
            return new PerformanceProbe(
                Stopwatch.GetTimestamp(),
                GC.GetAllocatedBytesForCurrentThread());
        }

        public void Complete(PerformanceProbe probe)
        {
            if (!probe.Enabled || _samples == null) return;
            var elapsed = Stopwatch.GetElapsedTime(probe.StartedTimestamp);
            _samples.Add(
                Math.Max(0, (long)(elapsed.TotalMilliseconds * 1000d)),
                Math.Max(0, GC.GetAllocatedBytesForCurrentThread() - probe.AllocatedBytesBefore));
        }

        public PerformanceChannelSnapshot Capture() => _samples == null
            ? default
            : _samples.Capture();
    }

    /// <summary>
    /// Thread-safe because provider channels publish from independent workers
    /// while the diagnostics timer snapshots them. Storage remains bounded.
    /// </summary>
    internal sealed class ConcurrentPerformanceRing
    {
        private readonly object _sync = new object();
        private readonly PerformanceSample[] _items;
        private int _next;
        private int _count;

        public ConcurrentPerformanceRing(int capacity)
        {
            _items = new PerformanceSample[Math.Max(1, capacity)];
        }

        public void Add(long durationMicroseconds, long allocatedBytes)
        {
            lock (_sync)
            {
                _items[_next] = new PerformanceSample(durationMicroseconds, allocatedBytes);
                _next = (_next + 1) % _items.Length;
                if (_count < _items.Length) _count++;
            }
        }

        public PerformanceChannelSnapshot Capture()
        {
            lock (_sync)
            {
                var durations = new long[_count];
                var allocations = new long[_count];
                var start = _count == _items.Length ? _next : 0;
                for (var index = 0; index < _count; index++)
                {
                    var sample = _items[(start + index) % _items.Length];
                    durations[index] = sample.DurationMicroseconds;
                    allocations[index] = sample.AllocatedBytes;
                }
                return new PerformanceChannelSnapshot(durations, allocations);
            }
        }

        private readonly struct PerformanceSample
        {
            public PerformanceSample(long durationMicroseconds, long allocatedBytes)
            {
                DurationMicroseconds = durationMicroseconds;
                AllocatedBytes = allocatedBytes;
            }

            public long DurationMicroseconds { get; }
            public long AllocatedBytes { get; }
        }
    }

    internal sealed class ConcurrentMetricRing
    {
        private readonly object _sync = new object();
        private readonly long[] _items;
        private int _next;
        private int _count;

        public ConcurrentMetricRing(int capacity)
        {
            _items = new long[Math.Max(1, capacity)];
        }

        public void Add(long value)
        {
            lock (_sync)
            {
                _items[_next] = value;
                _next = (_next + 1) % _items.Length;
                if (_count < _items.Length) _count++;
            }
        }

        public long[] ToArray()
        {
            lock (_sync)
            {
                var result = new long[_count];
                var start = _count == _items.Length ? _next : 0;
                for (var index = 0; index < _count; index++)
                    result[index] = _items[(start + index) % _items.Length];
                return result;
            }
        }
    }
}
