using System.Collections.Generic;

namespace Malco.Models
{
    internal sealed class CargoUnitCount
    {
        public int UnitId { get; set; }
        public string Name { get; set; }
        public string IconKey { get; set; }
        public int Count { get; set; }
    }

    internal sealed class UnitSpatialState
    {
        public UnitSpatialState()
        {
            Cargo = new List<CargoUnitCount>();
        }

        public string UnitTag { get; set; }
        public int UnitId { get; set; }
        public string Name { get; set; }
        public string IconKey { get; set; }
        public int MapX { get; set; }
        public int MapY { get; set; }
        public int? Energy { get; set; }
        public List<CargoUnitCount> Cargo { get; set; }
    }
}
