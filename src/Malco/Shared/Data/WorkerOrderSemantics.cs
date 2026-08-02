using System.Collections.Generic;
using Malco.Models;

namespace Malco.Data
{
    internal static class WorkerOrderSemantics
    {
        private const int StopOrderId = 1;
        private const int GuardOrderId = 2;
        private const int PlayerGuardOrderId = 3;
        private const int NothingOrderId = 23;
        private const int BurrowedOrderId = 117;
        private static readonly HashSet<int> IdleOrders = new HashSet<int>
        {
            StopOrderId,
            GuardOrderId,
            PlayerGuardOrderId,
            NothingOrderId,
            BurrowedOrderId
        };
        private static readonly HashSet<int> GasOrders = new HashSet<int> { 81, 82, 83, 84 };
        private static readonly HashSet<int> MineralOrders = new HashSet<int> { 79, 80, 85, 86, 87, 88, 89, 90 };
        private static readonly HashSet<int> MovementOrders = new HashSet<int> { 6, 49, 152 };
        private static readonly HashSet<int> CombatOrders = new HashSet<int> { 8, 9, 10, 14 };
        private static readonly HashSet<int> HoldOrders = new HashSet<int> { 107 };
        private static readonly HashSet<int> TransportOrders = new HashSet<int> { 92 };
        private static readonly HashSet<int> ScvExtraOrders = new HashSet<int> { 30, 33, 34 };
        private static readonly HashSet<int> DroneExtraOrders = new HashSet<int> { 25, 26 };
        private static readonly HashSet<int> ProbeExtraOrders = new HashSet<int> { 31 };

        public static bool IsIdle(int orderId) => IdleOrders.Contains(orderId);
        public static bool IsGas(int orderId) => GasOrders.Contains(orderId);
        public static bool IsMineral(int orderId) => MineralOrders.Contains(orderId);
        public static bool IsGenericHarvest(int orderId) => orderId == 79 || orderId == 80;
        public static bool IsResourceTransition(int orderId) => orderId == 91 || orderId == 151;
        public static bool IsResource(int orderId) => IsGas(orderId) || IsMineral(orderId);

        public static bool IsActive(Race race, int orderId)
        {
            if (IsResource(orderId) || MovementOrders.Contains(orderId) || CombatOrders.Contains(orderId) ||
                HoldOrders.Contains(orderId) || TransportOrders.Contains(orderId))
            {
                return true;
            }

            switch (race)
            {
                case Race.Terran: return ScvExtraOrders.Contains(orderId);
                case Race.Zerg: return DroneExtraOrders.Contains(orderId);
                case Race.Protoss: return ProbeExtraOrders.Contains(orderId);
                default: return false;
            }
        }
    }
}
