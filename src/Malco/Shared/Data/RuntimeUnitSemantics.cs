using System.Collections.Generic;

namespace Malco.Data
{
    internal static class RuntimeUnitSemantics
    {
        private static readonly HashSet<int> LiftedBuildingOrders =
            new HashSet<int> { 6, 13, 49, 70, 71, 72, 73, 74 };
        private static readonly HashSet<int> LiftableTerranBuildingUnitIds =
            new HashSet<int> { 106, 111, 113, 114, 116, 122 };

        public static bool IsLiftedBuilding(BwrApiRuntimeUnit unit)
        {
            return unit != null &&
                   LiftableTerranBuildingUnitIds.Contains(unit.UnitId) &&
                   LiftedBuildingOrders.Contains(unit.OrderId);
        }
    }
}
