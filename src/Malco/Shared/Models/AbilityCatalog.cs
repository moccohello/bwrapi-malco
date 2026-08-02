using System;
using System.Collections.Generic;
using System.Linq;
using Malco.Data;

namespace Malco.Models
{
    internal sealed class UnitAbilityDefinition
    {
        public UnitAbilityDefinition(int techId, string name, int energyCost, bool requiresResearch = true)
        {
            TechId = techId;
            Name = name ?? string.Empty;
            EnergyCost = energyCost;
            RequiresResearch = requiresResearch;
        }

        public int TechId { get; }
        public string Name { get; }
        public int EnergyCost { get; }
        public bool RequiresResearch { get; }
        public string Mode => "tech:" + TechId;
    }

    internal sealed class SpellcasterDefinition
    {
        public SpellcasterDefinition(Race race, int unitId, params UnitAbilityDefinition[] abilities)
        {
            Race = race;
            UnitId = unitId;
            var type = BwapiBroodWarTables.GetUnitTypeInfo(unitId);
            Name = type.Name;
            IconKey = type.IconKey;
            Abilities = Array.AsReadOnly(abilities ?? Array.Empty<UnitAbilityDefinition>());
        }

        public Race Race { get; }
        public int UnitId { get; }
        public string Name { get; }
        public string IconKey { get; }
        public IReadOnlyList<UnitAbilityDefinition> Abilities { get; }
    }

    internal static class AbilityCatalog
    {
        private static readonly IReadOnlyList<SpellcasterDefinition> Definitions = Array.AsReadOnly(new[]
        {
            new SpellcasterDefinition(Race.Terran, 1,
                new UnitAbilityDefinition(1, "Lockdown", 100),
                new UnitAbilityDefinition(10, "Personnel Cloaking", 25)),
            new SpellcasterDefinition(Race.Terran, 8,
                new UnitAbilityDefinition(9, "Cloaking Field", 25)),
            new SpellcasterDefinition(Race.Terran, 9,
                new UnitAbilityDefinition(6, "Defensive Matrix", 100, false),
                new UnitAbilityDefinition(2, "EMP Shockwave", 100),
                new UnitAbilityDefinition(7, "Irradiate", 75)),
            new SpellcasterDefinition(Race.Terran, 12,
                new UnitAbilityDefinition(8, "Yamato Gun", 150)),
            new SpellcasterDefinition(Race.Terran, 34,
                new UnitAbilityDefinition(24, "Restoration", 50),
                new UnitAbilityDefinition(30, "Optical Flare", 75)),
            new SpellcasterDefinition(Race.Terran, 107,
                new UnitAbilityDefinition(4, "Scanner Sweep", 50, false)),
            new SpellcasterDefinition(Race.Zerg, 45,
                new UnitAbilityDefinition(13, "Spawn Broodlings", 150),
                new UnitAbilityDefinition(17, "Ensnare", 75),
                new UnitAbilityDefinition(18, "Parasite", 75, false)),
            new SpellcasterDefinition(Race.Zerg, 46,
                new UnitAbilityDefinition(14, "Dark Swarm", 100, false),
                new UnitAbilityDefinition(15, "Plague", 150)),
            new SpellcasterDefinition(Race.Protoss, 60,
                new UnitAbilityDefinition(25, "Disruption Web", 125)),
            new SpellcasterDefinition(Race.Protoss, 63,
                new UnitAbilityDefinition(29, "Feedback", 50, false),
                new UnitAbilityDefinition(27, "Mind Control", 150),
                new UnitAbilityDefinition(31, "Maelstrom", 100)),
            new SpellcasterDefinition(Race.Protoss, 67,
                new UnitAbilityDefinition(19, "Psionic Storm", 75),
                new UnitAbilityDefinition(20, "Hallucination", 100)),
            new SpellcasterDefinition(Race.Protoss, 71,
                new UnitAbilityDefinition(21, "Recall", 150),
                new UnitAbilityDefinition(22, "Stasis Field", 100))
        });

        public static IEnumerable<SpellcasterDefinition> ForRace(Race race) =>
            Definitions.Where(item => item.Race == race);

        public static SpellcasterDefinition Find(int unitId) =>
            Definitions.FirstOrDefault(item => item.UnitId == unitId);
    }
}
