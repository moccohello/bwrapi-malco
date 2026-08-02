using System;
using System.Collections.Generic;
using System.Linq;

namespace Malco.Models
{
    internal sealed class GameSnapshot
    {
        public GameSnapshot()
            : this(
                DateTime.MinValue,
                false,
                Race.Unknown,
                -1,
                0,
                0,
                0,
                0,
                string.Empty,
                Array.Empty<UnitCount>(),
                Array.Empty<UnitCount>(),
                Array.Empty<GasWorkerGroup>(),
                Array.Empty<MineralWorkerGroup>(),
                Array.Empty<UpgradeState>(),
                Array.Empty<UpgradeState>(),
                Array.Empty<UnitSpatialState>())
        {
        }

        internal GameSnapshot(
            DateTime capturedAt,
            bool isInMatch,
            Race race,
            int localPlayerId,
            int workersTotal,
            int workersIdle,
            int workersActive,
            int workersUnknown,
            string workerStateStatus,
            IEnumerable<UnitCount> unitCounts,
            IEnumerable<UnitCount> buildingCounts,
            IEnumerable<GasWorkerGroup> gasWorkerGroups,
            IEnumerable<MineralWorkerGroup> mineralWorkerGroups,
            IEnumerable<UpgradeState> upgrades,
            IEnumerable<UpgradeState> availableUpgrades,
            IEnumerable<UnitSpatialState> unitSpatialStates)
        {
            CapturedAt = capturedAt;
            IsInMatch = isInMatch;
            Race = race;
            LocalPlayerId = localPlayerId;
            WorkersTotal = workersTotal;
            WorkersIdle = workersIdle;
            WorkersActive = workersActive;
            WorkersUnknown = workersUnknown;
            WorkerStateStatus = workerStateStatus ?? string.Empty;
            UnitCounts = Freeze(unitCounts);
            BuildingCounts = Freeze(buildingCounts);
            GasWorkerGroups = Freeze(gasWorkerGroups);
            MineralWorkerGroups = Freeze(mineralWorkerGroups);
            Upgrades = Freeze(upgrades);
            AvailableUpgrades = Freeze(availableUpgrades);
            UnitSpatialStates = Freeze(unitSpatialStates);
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

        public IReadOnlyList<UnitCount> UnitCounts { get; }

        public IReadOnlyList<UnitCount> BuildingCounts { get; }

        public IReadOnlyList<GasWorkerGroup> GasWorkerGroups { get; }

        public IReadOnlyList<MineralWorkerGroup> MineralWorkerGroups { get; }

        public IReadOnlyList<UpgradeState> Upgrades { get; }

        public IReadOnlyList<UpgradeState> AvailableUpgrades { get; }

        public IReadOnlyList<UnitSpatialState> UnitSpatialStates { get; }

        public GameSnapshot WithWorkerStateStatus(string status)
        {
            return new GameSnapshot(
                CapturedAt,
                IsInMatch,
                Race,
                LocalPlayerId,
                WorkersTotal,
                WorkersIdle,
                WorkersActive,
                WorkersUnknown,
                status,
                UnitCounts,
                BuildingCounts,
                GasWorkerGroups,
                MineralWorkerGroups,
                Upgrades,
                AvailableUpgrades,
                UnitSpatialStates);
        }

        private static IReadOnlyList<T> Freeze<T>(IEnumerable<T> values)
        {
            return Array.AsReadOnly((values ?? Array.Empty<T>()).ToArray());
        }
    }
}
