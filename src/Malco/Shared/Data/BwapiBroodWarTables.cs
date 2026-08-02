using System;

namespace Malco.Data
{
    /// <summary>
    /// BWAPI Brood War <c>UpgradeTypes::Enum</c> / <c>TechTypes::Enum</c> 순서·이름.
    /// 출처: bwapi/bwapi <c>UpgradeType.h</c>, <c>TechType.h</c> (인덱스 = Enum 정수값).
    /// </summary>
    internal static class BwapiBroodWarTables
    {
        private static readonly WorkerUnitTypeInfo[] WorkerUnitTypes =
        {
            new WorkerUnitTypeInfo(7, Malco.Models.Race.Terran),
            new WorkerUnitTypeInfo(41, Malco.Models.Race.Zerg),
            new WorkerUnitTypeInfo(64, Malco.Models.Race.Protoss)
        };

        private readonly struct WorkerUnitTypeInfo
        {
            public WorkerUnitTypeInfo(int unitTypeId, Malco.Models.Race race)
            {
                UnitTypeId = unitTypeId;
                Race = race;
            }

            public int UnitTypeId { get; }

            public Malco.Models.Race Race { get; }
        }

        public sealed class UnitTypeInfo
        {
            public UnitTypeInfo(string name, string iconKey, bool isBuilding)
            {
                Name = name;
                IconKey = iconKey;
                IsBuilding = isBuilding;
            }

            public string Name { get; private set; }

            public string IconKey { get; private set; }

            public bool IsBuilding { get; private set; }
        }

        /// <summary>BWAPI <c>UpgradeTypes::Enum</c> 마지막 데이터 인덱스 (<c>Upgrade_60</c>).</summary>
        public const int UpgradeTypeLastDataIndex = 60;

        /// <summary>슬롯 수 = 인덱스 0..60.</summary>
        public const int UpgradeTypeSlotCount = UpgradeTypeLastDataIndex + 1;

        /// <summary>BWAPI <c>TechTypes::Enum</c> — <c>Nuclear_Strike</c> = 45.</summary>
        public const int TechTypeLastDataIndex = 45;

        public const int TechTypeSlotCount = TechTypeLastDataIndex + 1;

        private static readonly string[] UpgradeTypeNames =
        {
            "Terran_Infantry_Armor",
            "Terran_Vehicle_Plating",
            "Terran_Ship_Plating",
            "Zerg_Carapace",
            "Zerg_Flyer_Carapace",
            "Protoss_Ground_Armor",
            "Protoss_Air_Armor",
            "Terran_Infantry_Weapons",
            "Terran_Vehicle_Weapons",
            "Terran_Ship_Weapons",
            "Zerg_Melee_Attacks",
            "Zerg_Missile_Attacks",
            "Zerg_Flyer_Attacks",
            "Protoss_Ground_Weapons",
            "Protoss_Air_Weapons",
            "Protoss_Plasma_Shields",
            "U_238_Shells",
            "Ion_Thrusters",
            "Unused_Upgrade_18",
            "Titan_Reactor",
            "Ocular_Implants",
            "Moebius_Reactor",
            "Apollo_Reactor",
            "Colossus_Reactor",
            "Ventral_Sacs",
            "Antennae",
            "Pneumatized_Carapace",
            "Metabolic_Boost",
            "Adrenal_Glands",
            "Muscular_Augments",
            "Grooved_Spines",
            "Gamete_Meiosis",
            "Metasynaptic_Node",
            "Singularity_Charge",
            "Leg_Enhancements",
            "Scarab_Damage",
            "Reaver_Capacity",
            "Gravitic_Drive",
            "Sensor_Array",
            "Gravitic_Boosters",
            "Khaydarin_Amulet",
            "Apial_Sensors",
            "Gravitic_Thrusters",
            "Carrier_Capacity",
            "Khaydarin_Core",
            "Unused_Upgrade_45",
            "Unused_Upgrade_46",
            "Argus_Jewel",
            "Unused_Upgrade_48",
            "Argus_Talisman",
            "Unused_Upgrade_50",
            "Caduceus_Reactor",
            "Chitinous_Plating",
            "Anabolic_Synthesis",
            "Charon_Boosters",
            "Unused_Upgrade_55",
            "Unused_Upgrade_56",
            "Unused_Upgrade_57",
            "Unused_Upgrade_58",
            "Unused_Upgrade_59",
            "Upgrade_60"
        };

        private static readonly string[] TechTypeNames =
        {
            "Stim_Packs",
            "Lockdown",
            "EMP_Shockwave",
            "Spider_Mines",
            "Scanner_Sweep",
            "Tank_Siege_Mode",
            "Defensive_Matrix",
            "Irradiate",
            "Yamato_Gun",
            "Cloaking_Field",
            "Personnel_Cloaking",
            "Burrowing",
            "Infestation",
            "Spawn_Broodlings",
            "Dark_Swarm",
            "Plague",
            "Consume",
            "Ensnare",
            "Parasite",
            "Psionic_Storm",
            "Hallucination",
            "Recall",
            "Stasis_Field",
            "Archon_Warp",
            "Restoration",
            "Disruption_Web",
            "Unused_26",
            "Mind_Control",
            "Dark_Archon_Meld",
            "Feedback",
            "Optical_Flare",
            "Maelstrom",
            "Lurker_Aspect",
            "Unused_33",
            "Healing",
            "Unused_Tech_35",
            "Unused_Tech_36",
            "Unused_Tech_37",
            "Unused_Tech_38",
            "Unused_Tech_39",
            "Unused_Tech_40",
            "Unused_Tech_41",
            "Unused_Tech_42",
            "Unused_Tech_43",
            "None",
            "Nuclear_Strike"
        };

        // Confirmed BWAPI 4.1.2 scalar static data. Upgrade time for level N is
        // base + factor * (N - 1); values are Brood War frames.
        private static readonly int[] UpgradeTimeBaseFrames =
        {
            4000, 4000, 4000, 4000, 4000, 4000, 4000, 4000, 4000, 4000, 4000, 4000, 4000, 4000, 4000, 4000,
            1500, 1500, 0, 2500, 2500, 2500, 2500, 2500, 2400, 2000, 2000, 1500, 1500, 1500, 1500, 2500,
            2500, 2500, 2000, 2500, 2500, 2500, 2000, 2000, 2500, 2500, 2500, 1500, 2500, 0, 0, 2500,
            0, 2500, 0, 2500, 2000, 2000, 2000, 0, 0, 0, 0, 0, 0
        };

        private static readonly int[] UpgradeTimeFactorFrames =
        {
            480, 480, 480, 480, 480, 480, 480, 480, 480, 480, 480, 480, 480, 480, 480, 480,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
        };

        private static readonly int[] TechResearchTimeFrames =
        {
            1200, 1500, 1800, 1200, 0, 1200, 0, 1200, 1800, 1500, 1200, 1200, 0, 1200, 0, 1500, 1500, 1200,
            0, 1800, 1200, 1800, 1500, 0, 1200, 1200, 0, 1800, 0, 0, 1800, 1500, 1800, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0
        };

        private static readonly System.Collections.Generic.Dictionary<int, UnitTypeInfo> UnitTypes =
            new System.Collections.Generic.Dictionary<int, UnitTypeInfo>
            {
                { 0, Unit("Marine", "terran/marine") },
                { 1, Unit("Ghost", "terran/ghost") },
                { 2, Unit("Vulture", "terran/vulture") },
                { 3, Unit("Goliath", "terran/goliath") },
                { 4, Unit("Goliath Turret", string.Empty) },
                { 5, Unit("Siege Tank", "terran/siege-tank") },
                { 6, Unit("Siege Tank Turret", string.Empty) },
                { 7, Unit("SCV", "terran/scv") },
                { 8, Unit("Wraith", "terran/wraith") },
                { 9, Unit("Science Vessel", "terran/science-vessel") },
                { 10, Unit("Gui Montag", "terran/firebat") },
                { 11, Unit("Dropship", "terran/dropship") },
                { 12, Unit("Battlecruiser", "terran/battlecruiser") },
                { 13, Unit("Spider Mine", "terran/spider-mine") },
                { 30, Unit("Siege Tank", "terran/siege-tank") },
                { 31, Unit("Siege Tank Turret", string.Empty) },
                { 32, Unit("Firebat", "terran/firebat") },
                { 34, Unit("Medic", "terran/medic") },
                { 35, Unit("Larva", "zerg/larva") },
                { 36, Unit("Egg", "zerg/cocoon") },
                { 37, Unit("Zergling", "zerg/zergling") },
                { 38, Unit("Hydralisk", "zerg/hydralisk") },
                { 39, Unit("Ultralisk", "zerg/ultralisk") },
                { 40, Unit("Broodling", "zerg/broodling") },
                { 41, Unit("Drone", "zerg/drone") },
                { 42, Unit("Overlord", "zerg/overlord") },
                { 43, Unit("Mutalisk", "zerg/mutalisk") },
                { 44, Unit("Guardian", "zerg/guardian") },
                { 45, Unit("Queen", "zerg/queen") },
                { 46, Unit("Defiler", "zerg/defiler") },
                { 47, Unit("Scourge", "zerg/scourge") },
                { 50, Unit("Infested Terran", "zerg/infested-terran") },
                { 58, Unit("Valkyrie", "terran/valkyrie") },
                { 59, Unit("Cocoon", "zerg/cocoon") },
                { 60, Unit("Corsair", "protoss/corsair") },
                { 61, Unit("Dark Templar", "protoss/dark-templar") },
                { 62, Unit("Devourer", "zerg/devourer") },
                { 63, Unit("Dark Archon", "protoss/dark-archon") },
                { 64, Unit("Probe", "protoss/probe") },
                { 65, Unit("Zealot", "protoss/zealot") },
                { 66, Unit("Dragoon", "protoss/dragoon") },
                { 67, Unit("High Templar", "protoss/high-templar") },
                { 68, Unit("Archon", "protoss/archon") },
                { 69, Unit("Shuttle", "protoss/shuttle") },
                { 70, Unit("Scout", "protoss/scout") },
                { 71, Unit("Arbiter", "protoss/arbiter") },
                { 72, Unit("Carrier", "protoss/carrier") },
                { 83, Unit("Reaver", "protoss/reaver") },
                { 84, Unit("Observer", "protoss/observer") },
                { 97, Unit("Lurker Egg", "zerg/cocoon") },
                { 103, Unit("Lurker", "zerg/lurker") },
                { 106, Building("Command Center", "terran/command-center") },
                { 107, Building("Comsat Station", "terran/comsat-station") },
                { 108, Building("Nuclear Silo", "terran/nuclear-silo") },
                { 109, Building("Supply Depot", "terran/supply-depot") },
                { 110, Building("Refinery", "terran/refinery") },
                { 111, Building("Barracks", "terran/barracks") },
                { 112, Building("Academy", "terran/academy") },
                { 113, Building("Factory", "terran/factory") },
                { 114, Building("Starport", "terran/starport") },
                { 115, Building("Control Tower", "terran/control-tower") },
                { 116, Building("Science Facility", "terran/science-facility") },
                { 117, Building("Covert Ops", "terran/covert-ops") },
                { 118, Building("Physics Lab", "terran/physics-lab") },
                { 120, Building("Machine Shop", "terran/machine-shop") },
                { 122, Building("Engineering Bay", "terran/engineering-bay") },
                { 123, Building("Armory", "terran/armory") },
                { 124, Building("Missile Turret", "terran/missile-turret") },
                { 125, Building("Bunker", "terran/bunker") },
                { 130, Building("Infested Command Center", "zerg/infested-command-center") },
                { 131, Building("Hatchery", "zerg/hatchery") },
                { 132, Building("Lair", "zerg/lair") },
                { 133, Building("Hive", "zerg/hive") },
                { 134, Building("Nydus Canal", "zerg/nydus-canal") },
                { 135, Building("Hydralisk Den", "zerg/hydralisk-den") },
                { 136, Building("Defiler Mound", "zerg/defiler-mound") },
                { 137, Building("Greater Spire", "zerg/greater-spire") },
                { 138, Building("Queen's Nest", "zerg/queens-nest") },
                { 139, Building("Evolution Chamber", "zerg/evolution-chamber") },
                { 140, Building("Ultralisk Cavern", "zerg/ultralisk-cavern") },
                { 141, Building("Spire", "zerg/spire") },
                { 142, Building("Spawning Pool", "zerg/spawning-pool") },
                { 143, Building("Creep Colony", "zerg/creep-colony") },
                { 144, Building("Spore Colony", "zerg/spore-colony") },
                { 146, Building("Sunken Colony", "zerg/sunken-colony") },
                { 149, Building("Extractor", "zerg/extractor") },
                { 154, Building("Nexus", "protoss/nexus") },
                { 155, Building("Robotics Facility", "protoss/robotics-facility") },
                { 156, Building("Pylon", "protoss/pylon") },
                { 157, Building("Assimilator", "protoss/assimilator") },
                { 159, Building("Observatory", "protoss/observatory") },
                { 160, Building("Gateway", "protoss/gateway") },
                { 162, Building("Photon Cannon", "protoss/photon-cannon") },
                { 163, Building("Citadel of Adun", "protoss/citadel-of-adun") },
                { 164, Building("Cybernetics Core", "protoss/cybernetics-core") },
                { 165, Building("Templar Archives", "protoss/templar-archives") },
                { 166, Building("Forge", "protoss/forge") },
                { 167, Building("Stargate", "protoss/stargate") },
                { 169, Building("Fleet Beacon", "protoss/fleet-beacon") },
                { 170, Building("Arbiter Tribunal", "protoss/arbiter-tribunal") },
                { 171, Building("Robotics Support Bay", "protoss/robotics-support-bay") },
                { 172, Building("Shield Battery", "protoss/shield-battery") }
            };

        public static UnitTypeInfo GetUnitTypeInfo(int unitTypeId)
        {
            UnitTypeInfo info;
            return UnitTypes.TryGetValue(unitTypeId, out info)
                ? info
                : new UnitTypeInfo("Unit " + unitTypeId.ToString(System.Globalization.CultureInfo.InvariantCulture), string.Empty, false);
        }

        public static bool IsKnownBuildingUnitId(int unitTypeId)
        {
            return GetUnitTypeInfo(unitTypeId).IsBuilding;
        }

        public static System.Collections.Generic.IEnumerable<int> WorkerUnitTypeIds
        {
            get
            {
                foreach (WorkerUnitTypeInfo workerUnitType in WorkerUnitTypes)
                {
                    yield return workerUnitType.UnitTypeId;
                }
            }
        }

        public static bool IsWorkerUnitId(int unitTypeId)
        {
            foreach (WorkerUnitTypeInfo workerUnitType in WorkerUnitTypes)
            {
                if (workerUnitType.UnitTypeId == unitTypeId)
                {
                    return true;
                }
            }

            return false;
        }

        public static Malco.Models.Race GetWorkerRace(int unitTypeId)
        {
            foreach (WorkerUnitTypeInfo workerUnitType in WorkerUnitTypes)
            {
                if (workerUnitType.UnitTypeId == unitTypeId)
                {
                    return workerUnitType.Race;
                }
            }

            return Malco.Models.Race.Unknown;
        }

        public static bool IsAuxiliarySubunit(int unitTypeId)
        {
            return unitTypeId == 4 ||
                   unitTypeId == 6 ||
                   unitTypeId == 18 ||
                   unitTypeId == 24 ||
                   unitTypeId == 26 ||
                   unitTypeId == 31 ||
                   unitTypeId == 73 ||
                   unitTypeId == 85;
        }

        private static UnitTypeInfo Unit(string name, string iconKey)
        {
            return new UnitTypeInfo(name, iconKey, false);
        }

        private static UnitTypeInfo Building(string name, string iconKey)
        {
            return new UnitTypeInfo(name, iconKey, true);
        }

        /// <summary>Melee BW: indices 0..15 are +1/+2/+3 lines; 16+ are one-time upgrades.</summary>
        public static int GetUpgradeMaxLevel(int upgradeIndex)
        {
            if (upgradeIndex < 0 || upgradeIndex >= UpgradeTypeSlotCount)
            {
                return 1;
            }

            return upgradeIndex <= 15 ? 3 : 1;
        }

        public static int GetUpgradeTimeFrames(int upgradeIndex, int level)
        {
            if (upgradeIndex < 0 || upgradeIndex >= UpgradeTimeBaseFrames.Length || level < 1)
            {
                return 0;
            }

            return UpgradeTimeBaseFrames[upgradeIndex] +
                   UpgradeTimeFactorFrames[upgradeIndex] * (level - 1);
        }

        public static int GetTechResearchTimeFrames(int techIndex)
        {
            return techIndex >= 0 && techIndex < TechResearchTimeFrames.Length
                ? TechResearchTimeFrames[techIndex]
                : 0;
        }

        public static double CalculateProgressPercent(int totalFrames, int remainingFrames)
        {
            if (totalFrames <= 0 || remainingFrames < 0)
            {
                return -1d;
            }

            return Math.Min(100d, Math.Max(0d, (totalFrames - remainingFrames) * 100d / totalFrames));
        }

        public static bool TryGetUpgradeIndex(string upgradeTypeName, out int index)
        {
            index = -1;
            if (string.IsNullOrEmpty(upgradeTypeName))
            {
                return false;
            }

            for (var i = 0; i < UpgradeTypeNames.Length; i++)
            {
                if (string.Equals(UpgradeTypeNames[i], upgradeTypeName, StringComparison.OrdinalIgnoreCase))
                {
                    index = i;
                    return true;
                }
            }

            return false;
        }

        public static bool TryGetTechIndex(string techTypeName, out int index)
        {
            index = -1;
            if (string.IsNullOrEmpty(techTypeName))
            {
                return false;
            }

            for (var i = 0; i < TechTypeNames.Length; i++)
            {
                if (string.Equals(TechTypeNames[i], techTypeName, StringComparison.OrdinalIgnoreCase))
                {
                    index = i;
                    return true;
                }
            }

            return false;
        }

        public static string GetUpgradeTypeName(int index)
        {
            if (index < 0 || index >= UpgradeTypeNames.Length)
            {
                return "?";
            }

            return UpgradeTypeNames[index];
        }

        public static string GetTechTypeName(int index)
        {
            if (index < 0 || index >= TechTypeNames.Length)
            {
                return "?";
            }

            return TechTypeNames[index];
        }
    }
}
