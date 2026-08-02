using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Malco.Data;
using Malco.Models;

namespace Malco
{
    internal enum TechTreeItemKind
    {
        Building,
        Unit,
        Upgrade,
        Tech
    }

    internal sealed class TechTreeRaceCatalog
    {
        public TechTreeRaceCatalog(Race race, string name, IEnumerable<TechTreeBranch> branches)
        {
            Race = race;
            Name = name;
            Branches = branches.ToList();
        }

        public Race Race { get; private set; }

        public string Name { get; private set; }

        public List<TechTreeBranch> Branches { get; private set; }
    }

    internal sealed class TechTreeBranch
    {
        public TechTreeBranch(int buildingUnitId, IEnumerable<TechTreeItem> items)
        {
            Building = TechTreeItem.Building(buildingUnitId);
            Items = items.ToList();
        }

        public TechTreeItem Building { get; private set; }

        public List<TechTreeItem> Items { get; private set; }
    }

    internal sealed class TechTreeItem
    {
        private TechTreeItem()
        {
        }

        public TechTreeItemKind Kind { get; private set; }

        public string Key { get; private set; }

        public string Name { get; private set; }

        public string IconKey { get; private set; }

        public int UnitId { get; private set; }

        public int UpgradeIndex { get; private set; }

        public int TechIndex { get; private set; }

        public bool SupportsAvailableAlert
        {
            get { return Kind == TechTreeItemKind.Upgrade || Kind == TechTreeItemKind.Tech; }
        }

        public bool SupportsCompletionAlert
        {
            get { return Kind == TechTreeItemKind.Upgrade || Kind == TechTreeItemKind.Tech; }
        }

        public UnitCount ToUnitCount()
        {
            return new UnitCount
            {
                UnitId = UnitId,
                Name = Name,
                IconKey = IconKey,
                Count = 1,
                IsBuilding = Kind == TechTreeItemKind.Building
            };
        }

        public UpgradeState ToUpgradeState()
        {
            if (Kind == TechTreeItemKind.Tech)
            {
                return new UpgradeState
                {
                    StateKey = Key,
                    Name = "Tech " + BwapiBroodWarTables.GetTechTypeName(TechIndex)
                };
            }

            return new UpgradeState
            {
                StateKey = Key,
                Name = "Upgrade " + BwapiBroodWarTables.GetUpgradeTypeName(UpgradeIndex)
            };
        }

        public static TechTreeItem Building(int unitId)
        {
            var info = BwapiBroodWarTables.GetUnitTypeInfo(unitId);
            return new TechTreeItem
            {
                Kind = TechTreeItemKind.Building,
                Key = BuildingKey(unitId),
                Name = info.Name,
                IconKey = info.IconKey,
                UnitId = unitId
            };
        }

        public static TechTreeItem Unit(int unitId)
        {
            var info = BwapiBroodWarTables.GetUnitTypeInfo(unitId);
            return new TechTreeItem
            {
                Kind = TechTreeItemKind.Unit,
                Key = UnitKey(unitId),
                Name = info.Name,
                IconKey = info.IconKey,
                UnitId = unitId
            };
        }

        public static TechTreeItem Upgrade(string upgradeTypeName)
        {
            int index;
            if (!BwapiBroodWarTables.TryGetUpgradeIndex(upgradeTypeName, out index))
            {
                throw new InvalidOperationException("Unknown upgrade type: " + upgradeTypeName);
            }

            return new TechTreeItem
            {
                Kind = TechTreeItemKind.Upgrade,
                Key = UpgradeKey(index),
                Name = DisplayName(upgradeTypeName),
                UpgradeIndex = index
            };
        }

        public static TechTreeItem Tech(string techTypeName)
        {
            int index;
            if (!BwapiBroodWarTables.TryGetTechIndex(techTypeName, out index))
            {
                throw new InvalidOperationException("Unknown tech type: " + techTypeName);
            }

            return new TechTreeItem
            {
                Kind = TechTreeItemKind.Tech,
                Key = TechKey(index),
                Name = DisplayName(techTypeName),
                TechIndex = index
            };
        }

        public static string BuildingKey(int unitId)
        {
            return "building:" + unitId.ToString(CultureInfo.InvariantCulture);
        }

        public static string UnitKey(int unitId)
        {
            return "unit:" + unitId.ToString(CultureInfo.InvariantCulture);
        }

        public static string UpgradeKey(int index)
        {
            return "upgrade:" + index.ToString(CultureInfo.InvariantCulture);
        }

        public static string TechKey(int index)
        {
            return "tech:" + index.ToString(CultureInfo.InvariantCulture);
        }

        private static string DisplayName(string value)
        {
            return (value ?? string.Empty).Replace('_', ' ');
        }
    }

    internal static class TechTreeCatalog
    {
        private static readonly Dictionary<Race, int[]> DisplayUnitIds =
            new Dictionary<Race, int[]>
            {
                {
                    Race.Terran,
                    new[] { 0, 1, 2, 3, 5, 7, 8, 9, 11, 12, 13, 30, 32, 34, 58 }
                },
                {
                    Race.Zerg,
                    new[] { 35, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 50, 62, 103 }
                },
                {
                    Race.Protoss,
                    new[] { 60, 61, 63, 64, 65, 66, 67, 68, 69, 70, 71, 72, 83, 84 }
                }
            };

        private static readonly List<TechTreeRaceCatalog> Catalogs = new List<TechTreeRaceCatalog>
        {
            new TechTreeRaceCatalog(
                Race.Terran,
                "Terran",
                new[]
                {
                    Branch(106, Unit(7)),
                    Branch(107),
                    Branch(108),
                    Branch(109),
                    Branch(110),
                    Branch(111, Unit(0), Unit(32), Unit(34), Unit(1)),
                    Branch(125),
                    Branch(112, Tech("Stim_Packs"), Upgrade("U_238_Shells"), Tech("Restoration"), Tech("Optical_Flare"), Upgrade("Caduceus_Reactor")),
                    Branch(122, Upgrade("Terran_Infantry_Weapons"), Upgrade("Terran_Infantry_Armor")),
                    Branch(124),
                    Branch(113, Unit(2), Unit(5), Unit(30), Unit(3)),
                    Branch(120, Tech("Spider_Mines"), Tech("Tank_Siege_Mode"), Upgrade("Ion_Thrusters")),
                    Branch(123, Upgrade("Terran_Vehicle_Weapons"), Upgrade("Terran_Vehicle_Plating"), Upgrade("Terran_Ship_Weapons"), Upgrade("Terran_Ship_Plating"), Upgrade("Charon_Boosters")),
                    Branch(114, Unit(8), Unit(11), Unit(9), Unit(12), Unit(58)),
                    Branch(115, Tech("Cloaking_Field"), Upgrade("Apollo_Reactor")),
                    Branch(116, Tech("EMP_Shockwave"), Tech("Irradiate"), Upgrade("Titan_Reactor")),
                    Branch(117, Tech("Lockdown"), Upgrade("Ocular_Implants"), Upgrade("Moebius_Reactor"), Tech("Personnel_Cloaking")),
                    Branch(118, Tech("Yamato_Gun"), Upgrade("Colossus_Reactor"))
                }),
            new TechTreeRaceCatalog(
                Race.Zerg,
                "Zerg",
                new[]
                {
                    Branch(131, Unit(41), Unit(42), Upgrade("Ventral_Sacs"), Upgrade("Antennae"), Upgrade("Pneumatized_Carapace")),
                    Branch(132),
                    Branch(133),
                    Branch(149),
                    Branch(142, Unit(37), Tech("Burrowing"), Upgrade("Metabolic_Boost"), Upgrade("Adrenal_Glands")),
                    Branch(139, Upgrade("Zerg_Melee_Attacks"), Upgrade("Zerg_Missile_Attacks"), Upgrade("Zerg_Carapace")),
                    Branch(135, Unit(38), Unit(103), Upgrade("Grooved_Spines"), Upgrade("Muscular_Augments"), Tech("Lurker_Aspect")),
                    Branch(141, Unit(43), Unit(47), Upgrade("Zerg_Flyer_Attacks"), Upgrade("Zerg_Flyer_Carapace")),
                    Branch(137, Unit(44), Unit(62)),
                    Branch(138, Unit(45), Unit(40), Tech("Spawn_Broodlings"), Tech("Ensnare"), Tech("Parasite"), Upgrade("Gamete_Meiosis")),
                    Branch(136, Unit(46), Tech("Dark_Swarm"), Tech("Consume"), Tech("Plague"), Upgrade("Metasynaptic_Node")),
                    Branch(140, Unit(39), Upgrade("Chitinous_Plating"), Upgrade("Anabolic_Synthesis")),
                    Branch(134),
                    Branch(143),
                    Branch(144),
                    Branch(146),
                    Branch(130, Unit(50))
                }),
            new TechTreeRaceCatalog(
                Race.Protoss,
                "Protoss",
                new[]
                {
                    Branch(154, Unit(64)),
                    Branch(156),
                    Branch(157),
                    Branch(160, Unit(65), Unit(66), Unit(67), Unit(61)),
                    Branch(166, Upgrade("Protoss_Ground_Weapons"), Upgrade("Protoss_Ground_Armor"), Upgrade("Protoss_Plasma_Shields")),
                    Branch(162),
                    Branch(164, Upgrade("Singularity_Charge"), Upgrade("Protoss_Air_Weapons"), Upgrade("Protoss_Air_Armor")),
                    Branch(163, Upgrade("Leg_Enhancements")),
                    Branch(165, Tech("Psionic_Storm"), Tech("Hallucination"), Upgrade("Khaydarin_Amulet"), Tech("Mind_Control"), Tech("Maelstrom"), Upgrade("Argus_Talisman")),
                    Branch(155, Unit(83), Unit(69), Unit(84)),
                    Branch(171, Upgrade("Scarab_Damage"), Upgrade("Reaver_Capacity"), Upgrade("Gravitic_Drive")),
                    Branch(159, Upgrade("Sensor_Array"), Upgrade("Gravitic_Boosters")),
                    Branch(167, Unit(70), Unit(60), Unit(72), Unit(71)),
                    Branch(169, Upgrade("Carrier_Capacity"), Upgrade("Apial_Sensors"), Upgrade("Gravitic_Thrusters"), Tech("Disruption_Web"), Upgrade("Argus_Jewel")),
                    Branch(170, Tech("Recall"), Tech("Stasis_Field"), Upgrade("Khaydarin_Core")),
                    Branch(172)
                })
        };

        public static IEnumerable<TechTreeRaceCatalog> All
        {
            get { return Catalogs; }
        }

        public static TechTreeRaceCatalog GetRaceCatalog(Race race)
        {
            return Catalogs.FirstOrDefault(catalog => catalog.Race == race) ?? Catalogs[0];
        }

        public static IEnumerable<TechTreeItem> GetDisplayUnits(Race race)
        {
            int[] unitIds;
            if (!DisplayUnitIds.TryGetValue(race, out unitIds))
            {
                unitIds = DisplayUnitIds[Race.Terran];
            }

            return unitIds.Select(Unit);
        }

        public static int GetDisplayOrder(Race race, string key)
        {
            var order = 0;
            foreach (var branch in GetRaceCatalog(race).Branches)
            {
                if (string.Equals(branch.Building.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    return order;
                }

                order++;
                foreach (var item in branch.Items)
                {
                    if (string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return order;
                    }

                    order++;
                }
            }

            return int.MaxValue;
        }

        public static int GetDisplayOrder(IEnumerable<Race> races, string key)
        {
            var groupOffset = 0;
            foreach (var race in (races ?? Enumerable.Empty<Race>()).Distinct())
            {
                var order = GetDisplayOrder(race, key);
                if (order != int.MaxValue)
                {
                    return groupOffset + order;
                }
                groupOffset += 1000;
            }
            return int.MaxValue;
        }

        private static TechTreeBranch Branch(int buildingUnitId, params TechTreeItem[] items)
        {
            return new TechTreeBranch(buildingUnitId, items);
        }

        private static TechTreeItem Unit(int unitId)
        {
            return TechTreeItem.Unit(unitId);
        }

        private static TechTreeItem Upgrade(string name)
        {
            return TechTreeItem.Upgrade(name);
        }

        private static TechTreeItem Tech(string name)
        {
            return TechTreeItem.Tech(name);
        }
    }
}
