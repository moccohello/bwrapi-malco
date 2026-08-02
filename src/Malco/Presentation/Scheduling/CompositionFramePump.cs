using System;
using System.Diagnostics;
using System.Threading;
using System.Windows.Media;
using System.Windows.Threading;
using Malco.Application.Contracts.Projection;
using Malco.Diagnostics;

namespace Malco.Presentation.Scheduling
{
    internal interface IOverlayFramePump
    {
        void RequestFrame();
        void SetProjectionEnabled(bool enabled);
        void SetAnimationEnabled(bool enabled);
    }

    internal readonly struct FramePumpCounters
    {
        public FramePumpCounters(
            long commits,
            long drains,
            long coalesced,
            long lastLatencyTicks,
            long[] applyLatencyTicks,
            int pendingFrame)
        {
            ProjectionCommits = commits;
            DispatcherDrains = drains;
            CoalescedSkips = coalesced;
            LastCommitToApplyTicks = lastLatencyTicks;
            ApplyLatencyTicks = applyLatencyTicks ?? Array.Empty<long>();
            PendingFrame = pendingFrame;
        }
        public long ProjectionCommits { get; }
        public long DispatcherDrains { get; }
        public long CoalescedSkips { get; }
        public long LastCommitToApplyTicks { get; }
        public long[] ApplyLatencyTicks { get; }
        public int PendingFrame { get; }
    }

    internal sealed class CompositionFramePump : IOverlayFramePump, IProjectionPresentationCommitSink, IDisposable
    {
        private readonly Dispatcher _dispatcher;
        private readonly Action _applyFrame;
        private readonly Action _drainAction;
        private readonly ConcurrentMetricRing _applyLatencyTicks;
        private int _projectionEnabled;
        private int _animationEnabled;
        private int _projectionDirty;
        private int _dirty;
        private int _queued;
        private int _stopped;
        private long _latestCommitTicks;
        private long _projectionCommits;
        private long _dispatcherDrains;
        private long _coalescedSkips;
        private long _lastLatencyTicks;
        private bool _renderingSubscribed;

        public CompositionFramePump(Dispatcher dispatcher, Action applyFrame)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _applyFrame = applyFrame ?? throw new ArgumentNullException(nameof(applyFrame));
            _drainAction = Drain;
            if (DiagnosticsSwitches.PerformanceEnabled)
                _applyLatencyTicks = new ConcurrentMetricRing(600);
        }

        public void MarkProjectionPresentationCommitted()
        {
            if (Volatile.Read(ref _stopped) != 0) return;
            Interlocked.Increment(ref _projectionCommits);
            Volatile.Write(ref _latestCommitTicks, Stopwatch.GetTimestamp());
            Interlocked.Exchange(ref _projectionDirty, 1);
            if (Volatile.Read(ref _projectionEnabled) != 0) MarkDirty();
        }

        public void RequestFrame()
        {
            if (Volatile.Read(ref _stopped) != 0) return;
            MarkDirty();
        }

        public void SetProjectionEnabled(bool enabled)
        {
            if (Volatile.Read(ref _stopped) != 0) return;
            var next = enabled ? 1 : 0;
            var previous = Interlocked.Exchange(ref _projectionEnabled, next);
            if (next != 0 && previous == 0)
            {
                Interlocked.Exchange(ref _projectionDirty, 1);
                RequestFrame();
            }
        }

        public void SetAnimationEnabled(bool enabled)
        {
            if (!_dispatcher.CheckAccess())
            {
                _dispatcher.BeginInvoke(
                    DispatcherPriority.Render,
                    new Action(() => SetAnimationEnabled(enabled)));
                return;
            }
            if (Volatile.Read(ref _stopped) != 0) enabled = false;
            Interlocked.Exchange(ref _animationEnabled, enabled ? 1 : 0);
            if (enabled && !_renderingSubscribed)
            {
                CompositionTarget.Rendering += OnCompositionRendering;
                _renderingSubscribed = true;
                RequestFrame();
            }
            else if (!enabled && _renderingSubscribed)
            {
                CompositionTarget.Rendering -= OnCompositionRendering;
                _renderingSubscribed = false;
            }
        }

        private void OnCompositionRendering(object sender, EventArgs args)
        {
            if (Volatile.Read(ref _animationEnabled) != 0) RequestFrame();
        }

        private void MarkDirty()
        {
            if (Interlocked.Exchange(ref _dirty, 1) != 0)
            {
                Interlocked.Increment(ref _coalescedSkips);
                return;
            }
            if (Interlocked.CompareExchange(ref _queued, 1, 0) == 0)
                _dispatcher.BeginInvoke(DispatcherPriority.Render, _drainAction);
        }

        private void Drain()
        {
            try
            {
                if (Volatile.Read(ref _stopped) == 0 && Interlocked.Exchange(ref _dirty, 0) != 0)
                {
                    var projectionDirty = Interlocked.Exchange(ref _projectionDirty, 0) != 0;
                    var committed = projectionDirty ? Volatile.Read(ref _latestCommitTicks) : 0;
                    Interlocked.Increment(ref _dispatcherDrains);
                    _applyFrame();
                    if (committed != 0)
                    {
                        long latency = Math.Max(0, Stopwatch.GetTimestamp() - committed);
                        Volatile.Write(ref _lastLatencyTicks, latency);
                        _applyLatencyTicks?.Add(latency);
                    }
                }
            }
            finally
            {
                Interlocked.Exchange(ref _queued, 0);
                if (Volatile.Read(ref _stopped) == 0 &&
                    Volatile.Read(ref _dirty) != 0 &&
                    Interlocked.CompareExchange(ref _queued, 1, 0) == 0)
                    _dispatcher.BeginInvoke(DispatcherPriority.Render, _drainAction);
            }
        }

        public FramePumpCounters GetCounters() => new FramePumpCounters(
            Volatile.Read(ref _projectionCommits),
            Volatile.Read(ref _dispatcherDrains),
            Volatile.Read(ref _coalescedSkips),
            Volatile.Read(ref _lastLatencyTicks),
            _applyLatencyTicks?.ToArray() ?? Array.Empty<long>(),
            Volatile.Read(ref _dirty) != 0 || Volatile.Read(ref _queued) != 0 ? 1 : 0);

        public void Stop()
        {
            if (Volatile.Read(ref _stopped) != 0) return;
            if (!_dispatcher.CheckAccess())
            {
                _dispatcher.Invoke(Stop);
                return;
            }
            SetAnimationEnabled(false);
            Volatile.Write(ref _stopped, 1);
            Interlocked.Exchange(ref _projectionEnabled, 0);
            Interlocked.Exchange(ref _animationEnabled, 0);
            Interlocked.Exchange(ref _projectionDirty, 0);
            Interlocked.Exchange(ref _dirty, 0);
        }

        public void Dispose() => Stop();
    }
}
