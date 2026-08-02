using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Malco.Models;

namespace Malco.Data
{
    internal sealed class FrozenSemanticSnapshot
    {
        private readonly ReadOnlyCollection<UnitCount> _unitCounts;
        private readonly ReadOnlyCollection<UnitCount> _buildingCounts;
        private readonly ReadOnlyCollection<GasWorkerGroup> _gasWorkerGroups;
        private readonly ReadOnlyCollection<MineralWorkerGroup> _mineralWorkerGroups;
        private readonly ReadOnlyCollection<UpgradeState> _upgrades;
        private readonly ReadOnlyCollection<UpgradeState> _availableUpgrades;
        private readonly ReadOnlyCollection<UnitSpatialState> _unitSpatialStates;

        private FrozenSemanticSnapshot(GameSnapshot source)
        {
            var snapshot = source ?? new GameSnapshot();
            CapturedAt = snapshot.CapturedAt;
            IsInMatch = snapshot.IsInMatch;
            Race = snapshot.Race;
            LocalPlayerId = snapshot.LocalPlayerId;
            WorkersTotal = snapshot.WorkersTotal;
            WorkersIdle = snapshot.WorkersIdle;
            WorkersActive = snapshot.WorkersActive;
            WorkersUnknown = snapshot.WorkersUnknown;
            WorkerStateStatus = snapshot.WorkerStateStatus ?? string.Empty;
            _unitCounts = Freeze(snapshot.UnitCounts, CloneUnitCount);
            _buildingCounts = Freeze(snapshot.BuildingCounts, CloneUnitCount);
            _gasWorkerGroups = Freeze(snapshot.GasWorkerGroups, CloneGasGroup);
            _mineralWorkerGroups = Freeze(snapshot.MineralWorkerGroups, CloneMineralGroup);
            _upgrades = Freeze(snapshot.Upgrades, CloneUpgrade);
            _availableUpgrades = Freeze(snapshot.AvailableUpgrades, CloneUpgrade);
            _unitSpatialStates = Freeze(snapshot.UnitSpatialStates, CloneUnitSpatialState);
        }

        public DateTime CapturedAt { get; }
        public bool IsInMatch { get; }
        public Race Race { get; }
        public int LocalPlayerId { get; }
        public int WorkersTotal { get; }
        public int WorkersIdle { get; }
        public int WorkersActive { get; }
        public int WorkersUnknown { get; }
        public string WorkerStateStatus { get; }
        public IReadOnlyList<UnitCount> UnitCounts { get { return _unitCounts; } }
        public IReadOnlyList<UnitCount> BuildingCounts { get { return _buildingCounts; } }
        public IReadOnlyList<GasWorkerGroup> GasWorkerGroups { get { return _gasWorkerGroups; } }
        public IReadOnlyList<MineralWorkerGroup> MineralWorkerGroups { get { return _mineralWorkerGroups; } }
        public IReadOnlyList<UpgradeState> Upgrades { get { return _upgrades; } }
        public IReadOnlyList<UpgradeState> AvailableUpgrades { get { return _availableUpgrades; } }
        public IReadOnlyList<UnitSpatialState> UnitSpatialStates { get { return _unitSpatialStates; } }

        public static FrozenSemanticSnapshot Freeze(GameSnapshot snapshot)
        {
            return new FrozenSemanticSnapshot(snapshot);
        }

        private static ReadOnlyCollection<T> Freeze<T>(IEnumerable<T> values, Func<T, T> clone)
            where T : class
        {
            return Array.AsReadOnly((values ?? Enumerable.Empty<T>())
                .Where(value => value != null)
                .Select(clone)
                .ToArray());
        }

        private static UnitCount CloneUnitCount(UnitCount value)
        {
            return new UnitCount
            {
                UnitId = value.UnitId,
                Name = value.Name,
                IconKey = value.IconKey,
                Count = value.Count,
                CompletedCount = value.CompletedCount,
                IsBuilding = value.IsBuilding
            };
        }

        private static GasWorkerGroup CloneGasGroup(GasWorkerGroup value)
        {
            return new GasWorkerGroup
            {
                GasIdentity = value.GasIdentity,
                UnitId = value.UnitId,
                MapX = value.MapX,
                MapY = value.MapY,
                WorkerCount = value.WorkerCount
            };
        }

        private static MineralWorkerGroup CloneMineralGroup(MineralWorkerGroup value)
        {
            return new MineralWorkerGroup
            {
                BaseIdentity = value.BaseIdentity,
                UnitId = value.UnitId,
                MapX = value.MapX,
                MapY = value.MapY,
                WorkerCount = value.WorkerCount,
                MineralPatchCount = value.MineralPatchCount
            };
        }

        private static UpgradeState CloneUpgrade(UpgradeState value)
        {
            return new UpgradeState
            {
                StateKey = value.StateKey,
                Name = value.Name,
                Level = value.Level,
                ProgressPercent = value.ProgressPercent,
                SecondsRemaining = value.SecondsRemaining,
                SecondsRemainingPrecise = value.SecondsRemainingPrecise,
                IsComplete = value.IsComplete,
                IsInProgress = value.IsInProgress,
                IsAvailable = value.IsAvailable,
                IsBlocked = value.IsBlocked
            };
        }

        private static UnitSpatialState CloneUnitSpatialState(UnitSpatialState value)
        {
            return new UnitSpatialState
            {
                UnitTag = value.UnitTag,
                UnitId = value.UnitId,
                Name = value.Name,
                IconKey = value.IconKey,
                MapX = value.MapX,
                MapY = value.MapY,
                Energy = value.Energy,
                Cargo = (value.Cargo ?? new List<CargoUnitCount>()).Select(item => new CargoUnitCount
                {
                    UnitId = item.UnitId,
                    Name = item.Name,
                    IconKey = item.IconKey,
                    Count = item.Count
                }).ToList()
            };
        }
    }
}
