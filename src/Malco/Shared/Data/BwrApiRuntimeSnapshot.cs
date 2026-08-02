using System;
using System.Collections.Generic;
using Malco.Models;

namespace Malco.Data
{
    internal sealed class BwrApiRuntimeSnapshot
    {
        public BwrApiRuntimeSnapshot()
        {
            Units = new List<BwrApiRuntimeUnit>();
            Upgrades = new List<BwrApiRuntimeUpgrade>();
            AvailableUpgrades = new List<BwrApiRuntimeUpgrade>();
            CompletedUnitCounts = new Dictionary<int, int>();
        }

        public DateTime CapturedAt { get; set; }

        public bool IsInMatch { get; set; }

        public Race Race { get; set; }

        public int PerspectivePlayerId { get; set; }

        public long PublicationSequence { get; set; }

        public bool HasReliableUpgradeState { get; set; } = true;

        public string Status { get; set; }

        public List<BwrApiRuntimeUnit> Units { get; private set; }

        public List<BwrApiRuntimeUpgrade> Upgrades { get; private set; }

        public List<BwrApiRuntimeUpgrade> AvailableUpgrades { get; private set; }

        public Dictionary<int, int> CompletedUnitCounts { get; private set; }
    }

    internal sealed class BwrApiRuntimeUnit
    {
        public string UnitTag { get; set; }

        public int UnitId { get; set; }

        public int OwnerId { get; set; }

        public StableIdentity SourceIdentity { get; set; }

        public int HitPointsRaw { get; set; }

        public int? ResourceAmount { get; set; }

        public int? EnergyRaw { get; set; }

        public string TransportUnitTag { get; set; }

        public bool? IsLoaded { get; set; }

        public string GasResourceUnitTag { get; set; }

        public int? RenderMapX { get; set; }

        public int? RenderMapY { get; set; }

        public string Name { get; set; }

        public string IconKey { get; set; }

        public bool IsBuilding { get; set; }

        public bool IsCompleted { get; set; }

        public bool IsWorker { get; set; }

        public bool IsSelected { get; set; }

        public bool IsHallucination { get; set; }

        public int MapX { get; set; }

        public int MapY { get; set; }

        public int OrderId { get; set; }

        public bool HasOrderTarget { get; set; }

        public int OrderTargetMapX { get; set; }

        public int OrderTargetMapY { get; set; }
    }

    internal sealed class BwrApiRuntimeUpgrade
    {
        public string StateKey { get; set; }

        public string Name { get; set; }

        public int Level { get; set; }

        public double ProgressPercent { get; set; }

        public int SecondsRemaining { get; set; }

        public double SecondsRemainingPrecise { get; set; }

        public bool IsComplete { get; set; }

        public bool IsInProgress { get; set; }

        public bool IsAvailable { get; set; }
    }
}
