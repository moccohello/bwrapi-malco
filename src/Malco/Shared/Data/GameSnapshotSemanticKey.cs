using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Malco.Models;

namespace Malco.Data
{
    internal static class GameSnapshotSemanticKey
    {
        public static string Build(GameSnapshot snapshot)
        {
            return Build(snapshot, null, null);
        }

        public static string Build(GameSnapshot snapshot, ProviderStatus? status, string message)
        {
            if (snapshot == null)
            {
                return "snapshot:null;status=" + FormatStatus(status) + ";message=" + (message ?? string.Empty);
            }

            return BuildCore(
                snapshot.IsInMatch,
                snapshot.Race,
                snapshot.LocalPlayerId,
                snapshot.WorkersTotal,
                snapshot.WorkersActive,
                snapshot.WorkersIdle,
                snapshot.WorkersUnknown,
                snapshot.UnitCounts,
                snapshot.BuildingCounts,
                snapshot.GasWorkerGroups,
                snapshot.MineralWorkerGroups,
                snapshot.UnitSpatialStates,
                snapshot.Upgrades,
                snapshot.AvailableUpgrades,
                status,
                message);
        }

        public static string Build(FrozenSemanticSnapshot snapshot, ProviderStatus? status, string message)
        {
            if (snapshot == null)
            {
                return "snapshot:null;status=" + FormatStatus(status) + ";message=" + (message ?? string.Empty);
            }

            return BuildCore(
                snapshot.IsInMatch,
                snapshot.Race,
                snapshot.LocalPlayerId,
                snapshot.WorkersTotal,
                snapshot.WorkersActive,
                snapshot.WorkersIdle,
                snapshot.WorkersUnknown,
                snapshot.UnitCounts,
                snapshot.BuildingCounts,
                snapshot.GasWorkerGroups,
                snapshot.MineralWorkerGroups,
                snapshot.UnitSpatialStates,
                snapshot.Upgrades,
                snapshot.AvailableUpgrades,
                status,
                message);
        }

        private static string BuildCore(
            bool isInMatch,
            Race race,
            int localPlayerId,
            int workersTotal,
            int workersActive,
            int workersIdle,
            int workersUnknown,
            IEnumerable<UnitCount> unitCounts,
            IEnumerable<UnitCount> buildingCounts,
            IEnumerable<GasWorkerGroup> gasGroups,
            IEnumerable<MineralWorkerGroup> mineralGroups,
            IEnumerable<UnitSpatialState> unitSpatialStates,
            IEnumerable<UpgradeState> upgrades,
            IEnumerable<UpgradeState> availableUpgrades,
            ProviderStatus? status,
            string message)
        {
            var builder = new StringBuilder(512);
            builder.Append("match=").Append(isInMatch ? "1" : "0");
            builder.Append(";status=").Append(FormatStatus(status));
            builder.Append(";message=").Append(message ?? string.Empty);
            builder.Append(";race=").Append((int)race);
            builder.Append(";player=").Append(localPlayerId.ToString(CultureInfo.InvariantCulture));
            builder.Append(";workers=")
                .Append(workersTotal.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(workersActive.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(workersIdle.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(workersUnknown.ToString(CultureInfo.InvariantCulture));

            AppendUnitCounts(builder, "units", unitCounts);
            AppendUnitCounts(builder, "buildings", buildingCounts);
            AppendGasGroups(builder, gasGroups);
            AppendMineralGroups(builder, mineralGroups);
            AppendUnitSpatialStates(builder, unitSpatialStates);
            AppendUpgrades(builder, "upgrades", upgrades);
            AppendUpgrades(builder, "available", availableUpgrades);

            return builder.ToString();
        }

        private static string FormatStatus(ProviderStatus? status)
        {
            return status.HasValue ? status.Value.ToString() : string.Empty;
        }

        private static void AppendUnitCounts(StringBuilder builder, string name, IEnumerable<UnitCount> counts)
        {
            builder.Append(';').Append(name).Append('=');
            foreach (var item in (counts ?? new UnitCount[0])
                .Where(item => item != null)
                .OrderBy(item => item.UnitId)
                .ThenBy(item => item.Name ?? string.Empty, StringComparer.Ordinal))
            {
                builder.Append(item.UnitId.ToString(CultureInfo.InvariantCulture)).Append(':')
                    .Append(item.Count.ToString(CultureInfo.InvariantCulture)).Append(':')
                    .Append(item.CompletedCount.ToString(CultureInfo.InvariantCulture)).Append(':')
                    .Append(item.IsBuilding ? "1" : "0").Append('|');
            }
        }

        private static void AppendGasGroups(StringBuilder builder, IEnumerable<GasWorkerGroup> groups)
        {
            builder.Append(";gas=");
            foreach (var item in (groups ?? new GasWorkerGroup[0])
                .Where(item => item != null)
                .OrderBy(item => item.GasIdentity))
            {
                builder.Append(item.GasIdentity.Value).Append(':')
                    .Append(item.UnitId.ToString(CultureInfo.InvariantCulture)).Append(':')
                    .Append(item.MapX.ToString(CultureInfo.InvariantCulture)).Append(':')
                    .Append(item.MapY.ToString(CultureInfo.InvariantCulture)).Append(':')
                    .Append(item.WorkerCount.ToString(CultureInfo.InvariantCulture)).Append('|');
            }
        }

        private static void AppendMineralGroups(StringBuilder builder, IEnumerable<MineralWorkerGroup> groups)
        {
            builder.Append(";minerals=");
            foreach (var item in (groups ?? new MineralWorkerGroup[0])
                .Where(item => item != null)
                .OrderBy(item => item.BaseIdentity))
            {
                builder.Append(item.BaseIdentity.Value).Append(':')
                    .Append(item.UnitId.ToString(CultureInfo.InvariantCulture)).Append(':')
                    .Append(item.MapX.ToString(CultureInfo.InvariantCulture)).Append(':')
                    .Append(item.MapY.ToString(CultureInfo.InvariantCulture)).Append(':')
                    .Append(item.WorkerCount.ToString(CultureInfo.InvariantCulture)).Append(':')
                    .Append(item.MineralPatchCount.ToString(CultureInfo.InvariantCulture)).Append('|');
            }
        }

        private static void AppendUpgrades(StringBuilder builder, string name, IEnumerable<UpgradeState> states)
        {
            builder.Append(';').Append(name).Append('=');
            foreach (var item in (states ?? new UpgradeState[0])
                .Where(item => item != null)
                .OrderBy(item => item.StateKey ?? item.Name ?? string.Empty, StringComparer.Ordinal))
            {
                builder.Append(item.StateKey ?? item.Name ?? string.Empty).Append(':')
                    .Append(item.Level.ToString(CultureInfo.InvariantCulture)).Append(':')
                    .Append(item.ProgressPercent.ToString("0.###", CultureInfo.InvariantCulture)).Append(':')
                    .Append(item.SecondsRemaining.ToString(CultureInfo.InvariantCulture)).Append(':')
                    .Append(item.SecondsRemainingPrecise.ToString("0.###", CultureInfo.InvariantCulture)).Append(':')
                    .Append(item.IsComplete ? "1" : "0").Append(':')
                    .Append(item.IsInProgress ? "1" : "0").Append(':')
                    .Append(item.IsAvailable ? "1" : "0").Append(':')
                    .Append(item.IsBlocked ? "1" : "0").Append('|');
            }
        }

        private static void AppendUnitSpatialStates(StringBuilder builder, IEnumerable<UnitSpatialState> states)
        {
            builder.Append(";spatial-units=");
            foreach (var item in (states ?? new UnitSpatialState[0])
                .Where(item => item != null)
                .OrderBy(item => item.UnitTag ?? string.Empty, StringComparer.Ordinal))
            {
                builder.Append(item.UnitTag ?? string.Empty).Append(':')
                    .Append(item.UnitId.ToString(CultureInfo.InvariantCulture)).Append(':')
                    .Append(item.MapX.ToString(CultureInfo.InvariantCulture)).Append(':')
                    .Append(item.MapY.ToString(CultureInfo.InvariantCulture)).Append(':')
                    .Append(item.Energy.HasValue ? item.Energy.Value.ToString(CultureInfo.InvariantCulture) : "-")
                    .Append('[');
                foreach (var cargo in (item.Cargo ?? new List<CargoUnitCount>()).OrderBy(cargo => cargo.UnitId))
                {
                    builder.Append(cargo.UnitId.ToString(CultureInfo.InvariantCulture)).Append('x')
                        .Append(cargo.Count.ToString(CultureInfo.InvariantCulture)).Append(',');
                }
                builder.Append("]|");
            }
        }
    }
}
