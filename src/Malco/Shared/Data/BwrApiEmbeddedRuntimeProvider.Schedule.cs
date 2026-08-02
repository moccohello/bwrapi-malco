using System;
using System.Diagnostics;
using System.Linq;
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

        private static int ComputePollWaitMilliseconds(long now, params long[] deadlines)
        {
            long next = deadlines.Where(value => value != long.MaxValue).DefaultIfEmpty(long.MaxValue).Min();
            if (next == long.MaxValue) return Timeout.Infinite;
            long remaining = next - now;
            if (remaining <= 0) return 0;
            long milliseconds = (remaining * 1000L + Stopwatch.Frequency - 1L) / Stopwatch.Frequency;
            return (int)Math.Max(1L, Math.Min(50L, milliseconds));
        }
    }
}
