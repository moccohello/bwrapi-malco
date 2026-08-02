namespace Malco.Models
{
    internal sealed class GasWorkerGroup
    {
        public StableIdentity GasIdentity { get; init; }

        public int UnitId { get; init; }

        public int MapX { get; init; }

        public int MapY { get; init; }

        public int WorkerCount { get; init; }
    }
}
