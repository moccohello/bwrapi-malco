using System.Collections.Generic;

namespace Malco.Data
{
    /// <summary>버로우 아이콘을 표시할 수 있는 저그 유닛 id.</summary>
    internal static class BurrowableUnitCatalog
    {
        public static readonly IReadOnlyList<int> UnitIds = new[] { 41, 37, 38, 46, 103, 50 };

        public static bool IsKnown(int unitId)
        {
            for (var i = 0; i < UnitIds.Count; i++)
            {
                if (UnitIds[i] == unitId)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
