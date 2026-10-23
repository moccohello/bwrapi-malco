using System.Collections.Generic;

namespace Malco.Models
{
    internal sealed class CargoUnitCount
    {
        public int UnitId { get; set; }
        public string Name { get; set; }
        public string IconKey { get; set; }
        public int Count { get; set; }
    }

    internal sealed class UnitEffect
    {
        public string Kind { get; set; }
        public int RemainingFrames { get; set; }
        public int? RemainingDamage { get; set; }
    }

    internal sealed class UnitSpatialState
    {
        public UnitSpatialState()
        {
            Cargo = new List<CargoUnitCount>();
            Effects = new List<UnitEffect>();
            BurrowedUnits = new List<CargoUnitCount>();
        }

        public string UnitTag { get; set; }
        public int UnitId { get; set; }
        public string Name { get; set; }
        public string IconKey { get; set; }
        public int MapX { get; set; }
        public int MapY { get; set; }
        public int HitPointsRaw { get; set; }
        public int? Energy { get; set; }
        public bool IsBuilding { get; set; }
        public bool? IsCompleted { get; set; }
        public bool? IsPowered { get; set; }
        public bool? IsInterruptible { get; set; }
        public byte? LockdownTimer { get; set; }
        public byte? MaelstromTimer { get; set; }
        public byte? StasisTimer { get; set; }
        public bool? IsLoaded { get; set; }
        public int OrderId { get; set; }
        public byte? SecondaryOrderId { get; set; }
        public byte? SpiderMineCount { get; set; }

        public uint? LarvaRemainingFrames { get; set; }

        public int? ConnectedLarvaCount { get; set; }
        public int? MorphTargetUnitId { get; set; }
        public int? CompletedNuclearMissileCount { get; set; }
        public List<CargoUnitCount> Cargo { get; set; }
        public List<UnitEffect> Effects { get; set; }
        // Burrowed Zerg units stacked on this exact spot.
        public List<CargoUnitCount> BurrowedUnits { get; set; }
    }
}
