using System;
using System.Threading;
using System.Windows.Threading;
using Malco.Application.Contracts.Output;
using Malco.Data;

namespace Malco.Presentation.Scheduling
{
    [Flags]
    internal enum PresentationDirtyMask
    {
        None = 0,
        StateCommit = 1,
        Clock = 2
    }

    internal sealed class DispatcherPresentationScheduler : IOverlayStateCommitSink
    {
        private readonly Dispatcher _dispatcher;
        private readonly Action<PresentationDirtyMask> _apply;
        private readonly Action _drainDelegate;
        private int _dirtyMask;
        private int _drainQueued;
        private int _closing;

        public DispatcherPresentationScheduler(
            Dispatcher dispatcher,
            Action<PresentationDirtyMask> apply)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _apply = apply ?? throw new ArgumentNullException(nameof(apply));
            _drainDelegate = Drain;
        }

        public void MarkOverlayStateCommitted(OverlayReadModel state) => Mark(PresentationDirtyMask.StateCommit);

        public void MarkClock() => Mark(PresentationDirtyMask.Clock);

        public void Stop()
        {
            Volatile.Write(ref _closing, 1);
            Interlocked.Exchange(ref _dirtyMask, 0);
        }

        private void Mark(PresentationDirtyMask mask)
        {
            if (mask == PresentationDirtyMask.None || Volatile.Read(ref _closing) != 0) return;
            Interlocked.Or(ref _dirtyMask, (int)mask);
            if (Interlocked.CompareExchange(ref _drainQueued, 1, 0) == 0)
            {
                _dispatcher.BeginInvoke(DispatcherPriority.Render, _drainDelegate);
            }
        }

        private void Drain()
        {
            try
            {
                while (Volatile.Read(ref _closing) == 0)
                {
                    var dirty = (PresentationDirtyMask)Interlocked.Exchange(ref _dirtyMask, 0);
                    if (dirty != PresentationDirtyMask.None) _apply(dirty);
                    if (Volatile.Read(ref _dirtyMask) == 0) return;
                }
            }
            finally
            {
                Interlocked.Exchange(ref _drainQueued, 0);
                if (Volatile.Read(ref _closing) != 0)
                {
                    Interlocked.Exchange(ref _dirtyMask, 0);
                }
                else if (Volatile.Read(ref _dirtyMask) != 0 &&
                    Interlocked.CompareExchange(ref _drainQueued, 1, 0) == 0)
                {
                    _dispatcher.BeginInvoke(DispatcherPriority.Render, _drainDelegate);
                }
            }
        }
    }
}
