using System.Threading;
using Malco.Application.Contracts.Projection;

namespace Malco.Application.Projection
{
    internal sealed class ProjectionMailbox : IProjectionMailboxReader
    {
        private long _version;
        private int _hasSample;
        private ProjectionSample _sample;

        public void Publish(in ProjectionSample sample)
        {
            // Providers have one projection producer. Odd versions are being
            // written; even versions are stable coherent struct snapshots.
            var writeVersion = Interlocked.Increment(ref _version);
            _sample = sample;
            Volatile.Write(ref _hasSample, 1);
            Volatile.Write(ref _version, writeVersion + 1L);
        }

        public bool TryReadLatest(out ProjectionSample sample)
        {
            var spinner = new SpinWait();
            while (true)
            {
                var before = Volatile.Read(ref _version);
                if ((before & 1L) != 0L)
                {
                    spinner.SpinOnce();
                    continue;
                }

                var captured = _sample;
                var hasSample = Volatile.Read(ref _hasSample) != 0;
                Thread.MemoryBarrier();
                var after = Volatile.Read(ref _version);
                if (before == after && (after & 1L) == 0L)
                {
                    sample = captured;
                    return hasSample;
                }

                spinner.SpinOnce();
            }
        }
    }
}
