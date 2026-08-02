using System;

namespace Malco
{
    internal sealed partial class OverlayHudMetrics
    {
        private sealed class MetricRingBuffer
        {
            private readonly long[] _items;
            private int _next;
            private int _count;

            public MetricRingBuffer(int capacity)
            {
                _items = new long[Math.Max(1, capacity)];
            }

            public void Add(long value)
            {
                _items[_next] = value;
                _next = (_next + 1) % _items.Length;
                if (_count < _items.Length)
                {
                    _count++;
                }
            }

            public long[] ToArray()
            {
                var result = new long[_count];
                var start = _count == _items.Length ? _next : 0;
                for (var index = 0; index < _count; index++)
                {
                    result[index] = _items[(start + index) % _items.Length];
                }
                return result;
            }
        }
    }
}
