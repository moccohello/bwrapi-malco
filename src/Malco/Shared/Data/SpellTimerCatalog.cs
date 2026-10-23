using System;
using System.Collections.Generic;
using System.Linq;
using Malco.Models;

namespace Malco.Data
{
    /// <summary>종족별로 남은 시간을 표시할 수 있는 지속 마법 종류. 문자열은 효과 종류 이름과 같다.</summary>
    internal static class SpellTimerCatalog
    {
        private static readonly string[] Terran = { "Lockdown", "Irradiate", "Defensive Matrix" };
        private static readonly string[] Protoss = { "Psionic Storm", "Stasis Field", "Maelstrom", "Disruption Web" };
        private static readonly string[] Zerg = { "Dark Swarm", "Ensnare", "Plague" };

        public static IReadOnlyList<string> ForRace(Race race)
        {
            switch (race)
            {
                case Race.Terran:
                    return Terran;
                case Race.Protoss:
                    return Protoss;
                case Race.Zerg:
                    return Zerg;
                default:
                    return Array.Empty<string>();
            }
        }

        public static bool IsKnown(string kind) =>
            !string.IsNullOrEmpty(kind) &&
            Terran.Concat(Protoss).Concat(Zerg).Contains(kind, StringComparer.Ordinal);
    }
}
