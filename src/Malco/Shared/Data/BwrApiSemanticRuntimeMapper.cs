using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BwrApi.Client;
using Malco.Models;

namespace Malco.Data
{
    internal sealed class BwrApiSemanticRuntimeMapper
    {
        private int? _replayPerspectivePlayerId;

        public void ResetSessionState()
        {
            _replayPerspectivePlayerId = null;
        }

        public BwrApiRuntimeSnapshot Map(
            BwrApiFrameHeader header,
            BwrApiSemanticSnapshotV1 source)
        {
            bool isReplay = source.Session?.IsReplay == true;
            int perspectivePlayerId = ResolvePerspectivePlayerId(source, isReplay);
            BwrApiSemanticPlayerV1 perspectivePlayer =
                source.Players.FirstOrDefault(player => player.PlayerId == perspectivePlayerId);
            Race race = ConvertRace(perspectivePlayer?.Race);
            var runtime = new BwrApiRuntimeSnapshot
            {
                CapturedAt = DateTime.Now,
                // GameReady is the runtime readiness authority. Battle.net
                // match state is nullable metadata and must not veto LAN,
                // single-player, replay, or other ready sessions.
                IsInMatch = source.Session?.GameReady == true,
                Race = race,
                PerspectivePlayerId = perspectivePlayerId,
                PublicationSequence =
                    checked((long)header.PublicationSequence),
                HasReliableUpgradeState = source.UpgradeStateReliable,
                Status = source.Message ?? string.Empty
            };

            foreach (BwrApiSemanticUnitV1 unit in source.Units)
            {
                BwapiBroodWarTables.UnitTypeInfo type =
                    BwapiBroodWarTables.GetUnitTypeInfo(unit.UnitTypeId);
                runtime.Units.Add(new BwrApiRuntimeUnit
                {
                    UnitTag = unit.UnitTag,
                    UnitId = unit.UnitTypeId,
                    OwnerId = unit.OwnerId,
                    SourceIdentity = StableIdentity.FromUnitTag(unit.UnitTag),
                    HitPointsRaw = unit.HitPointsRaw,
                    ResourceAmount = unit.ResourceAmount,
                    EnergyRaw = unit.EnergyRaw,
                    TransportUnitTag = unit.TransportUnitTag,
                    IsLoaded = unit.IsLoaded,
                    GasResourceUnitTag = unit.GasResourceUnitTag,
                    RenderMapX = unit.RenderMapPosition?.X,
                    RenderMapY = unit.RenderMapPosition?.Y,
                    Name = type.Name,
                    IconKey = type.IconKey,
                    IsBuilding = type.IsBuilding,
                    IsCompleted = unit.IsCompleted == true,
                    IsWorker = BwapiBroodWarTables.IsWorkerUnitId(unit.UnitTypeId),
                    IsSelected = unit.IsSelected,
                    IsHallucination = unit.IsHallucination,
                    MapX = unit.X,
                    MapY = unit.Y,
                    OrderId = unit.OrderId,
                    HasOrderTarget = unit.HasOrderTarget,
                    OrderTargetMapX = unit.OrderTargetX,
                    OrderTargetMapY = unit.OrderTargetY
                });
            }

            foreach (BwrApiSemanticUnitCountV1 count in source.CompletedUnitCounts)
            {
                if (count.PlayerId != perspectivePlayerId || count.CompletedCount > int.MaxValue)
                {
                    continue;
                }

                runtime.CompletedUnitCounts[count.UnitTypeId] =
                    checked((int)count.CompletedCount);
            }

            if (source.UpgradeStateReliable)
            {
                foreach (BwrApiSemanticUpgradeV1 upgrade in source.Upgrades.Where(
                             item => item.PlayerId == perspectivePlayerId))
                {
                    AddUpgrade(runtime, upgrade);
                }

                foreach (BwrApiSemanticTechV1 tech in source.Techs.Where(
                             item => item.PlayerId == perspectivePlayerId))
                {
                    AddTech(runtime, tech);
                }
            }

            return runtime;
        }

        private int ResolvePerspectivePlayerId(
            BwrApiSemanticSnapshotV1 source,
            bool isReplay)
        {
            if (!isReplay)
            {
                _replayPerspectivePlayerId = null;
                return source.LocalPlayerId.HasValue ? source.LocalPlayerId.Value : -1;
            }

            HashSet<int> participantIds = source.Players
                .Select(player => (int)player.PlayerId)
                .ToHashSet();
            int[] selectedOwnerIds = source.Units
                .Where(unit => unit.IsSelected && participantIds.Contains(unit.OwnerId))
                .Select(unit => (int)unit.OwnerId)
                .Distinct()
                .ToArray();
            if (selectedOwnerIds.Length == 1)
            {
                _replayPerspectivePlayerId = selectedOwnerIds[0];
            }

            if (!_replayPerspectivePlayerId.HasValue ||
                !participantIds.Contains(_replayPerspectivePlayerId.Value))
            {
                _replayPerspectivePlayerId = participantIds.Count > 0
                    ? participantIds.Min()
                    : (int?)null;
            }

            return _replayPerspectivePlayerId ?? -1;
        }

        private static void AddUpgrade(
            BwrApiRuntimeSnapshot runtime,
            BwrApiSemanticUpgradeV1 source)
        {
            int level = source.Level ?? 0;
            int remaining = source.RemainingFrames ?? 0;
            if (level > 0)
            {
                runtime.Upgrades.Add(BuildUpgrade(source.UpgradeTypeId, level, false, 0));
            }

            if (source.InProgress == true && source.RemainingFrames.HasValue)
            {
                int nextLevel = Math.Max(1, level + 1);
                runtime.Upgrades.Add(
                    BuildUpgrade(source.UpgradeTypeId, nextLevel, true, remaining));
            }
        }

        private static BwrApiRuntimeUpgrade BuildUpgrade(
            int id,
            int level,
            bool inProgress,
            int remaining)
        {
            string name = "Upgrade " + BwapiBroodWarTables.GetUpgradeTypeName(id);
            if (BwapiBroodWarTables.GetUpgradeMaxLevel(id) > 1)
            {
                name += " +" + level.ToString(CultureInfo.InvariantCulture);
            }

            return new BwrApiRuntimeUpgrade
            {
                StateKey = "upgrade:" + id.ToString(CultureInfo.InvariantCulture),
                Name = name,
                Level = level,
                ProgressPercent = inProgress
                    ? BwapiBroodWarTables.CalculateProgressPercent(
                        BwapiBroodWarTables.GetUpgradeTimeFrames(id, level),
                        remaining)
                    : 100d,
                SecondsRemaining = remaining > 0
                    ? (int)Math.Ceiling(remaining / 24d)
                    : 0,
                SecondsRemainingPrecise = remaining / 24d,
                IsComplete = !inProgress,
                IsInProgress = inProgress
            };
        }

        private static void AddTech(
            BwrApiRuntimeSnapshot runtime,
            BwrApiSemanticTechV1 source)
        {
            bool researched = source.Researched == true;
            bool hasUsableProgress =
                source.InProgress == true && source.RemainingFrames.HasValue;
            int remaining = source.RemainingFrames ?? 0;
            var value = new BwrApiRuntimeUpgrade
            {
                StateKey =
                    "tech:" + source.TechTypeId.ToString(CultureInfo.InvariantCulture),
                Name = "Tech " + BwapiBroodWarTables.GetTechTypeName(source.TechTypeId),
                Level = researched ? 1 : 0,
                ProgressPercent = researched
                    ? 100d
                    : BwapiBroodWarTables.CalculateProgressPercent(
                        BwapiBroodWarTables.GetTechResearchTimeFrames(source.TechTypeId),
                        remaining),
                SecondsRemaining = remaining > 0
                    ? (int)Math.Ceiling(remaining / 24d)
                    : 0,
                SecondsRemainingPrecise = remaining / 24d,
                IsComplete = researched,
                IsInProgress = hasUsableProgress,
                IsAvailable = source.Available == true && !researched
            };
            if (researched || hasUsableProgress)
            {
                runtime.Upgrades.Add(value);
            }
            else if (value.IsAvailable)
            {
                runtime.AvailableUpgrades.Add(value);
            }
        }

        private static Race ConvertRace(byte? race) => race switch
        {
            0 => Race.Zerg,
            1 => Race.Terran,
            2 => Race.Protoss,
            _ => Race.Unknown
        };
    }
}
