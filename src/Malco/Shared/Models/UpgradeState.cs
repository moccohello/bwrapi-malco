namespace Malco.Models
{
    internal sealed class UpgradeState
    {
        public string StateKey { get; init; }

        public string Name { get; init; }

        public int Level { get; init; }

        public double ProgressPercent { get; init; }

        public int SecondsRemaining { get; init; }

        public double SecondsRemainingPrecise { get; init; }

        public bool IsComplete { get; init; }

        public bool IsInProgress { get; init; }

        public bool IsAvailable { get; init; }

        public bool IsBlocked { get; init; }
    }
}
