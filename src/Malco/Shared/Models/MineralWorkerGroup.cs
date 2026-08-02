namespace Malco.Models
{
    internal sealed class MineralWorkerGroup
    {
        public StableIdentity BaseIdentity { get; init; }

        public int UnitId { get; init; }

        public int MapX { get; init; }

        public int MapY { get; init; }

        public int WorkerCount { get; init; }

        public int MineralPatchCount { get; init; }
    }
}
