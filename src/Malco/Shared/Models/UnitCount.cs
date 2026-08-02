namespace Malco.Models
{
    internal sealed class UnitCount
    {
        public int UnitId { get; init; }

        public string Name { get; init; }

        public string IconKey { get; init; }

        public int Count { get; init; }

        public int CompletedCount { get; init; }

        public bool IsBuilding { get; init; }
    }
}
