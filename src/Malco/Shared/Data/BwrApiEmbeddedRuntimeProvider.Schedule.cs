using System;
using System.Diagnostics;
using System.Threading;

namespace Malco.Data
{
    internal sealed partial class BwrApiEmbeddedRuntimeProvider
    {
        private static long EnableDeadline(bool enabled, long deadline, long now) =>
            !enabled ? long.MaxValue : deadline == long.MaxValue ? now : deadline;

        private static bool IsDue(long deadline, long now) =>
            deadline != long.MaxValue && deadline <= now;

        private static long NextDeadline(long now, TimeSpan interval)
        {
            long delta = Math.Max(1L, (long)Math.Ceiling(interval.TotalSeconds * Stopwatch.Frequency));
            return now > long.MaxValue - delta ? long.MaxValue : now + delta;
        }

        private static int ComputePollWaitMilliseconds(
            long now,
            long firstDeadline,
            long secondDeadline = long.MaxValue)
        {
            var deadline = Math.Min(firstDeadline, secondDeadline);
            if (deadline == long.MaxValue) return Timeout.Infinite;
            long remaining = deadline - now;
            if (remaining <= 0) return 0;
            long milliseconds = (remaining * 1000L + Stopwatch.Frequency - 1L) / Stopwatch.Frequency;
            return (int)Math.Max(1L, Math.Min(50L, milliseconds));
        }

    }
}
