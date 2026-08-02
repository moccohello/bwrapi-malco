using System;

namespace Malco.Data
{
    internal static class RuntimeUnitCoordinates
    {
        public static Tuple<int, int> Resolve(BwrApiRuntimeUnit unit)
        {
            return unit.RenderMapX.HasValue && unit.RenderMapY.HasValue &&
                   (unit.RenderMapX.Value != 0 || unit.RenderMapY.Value != 0)
                ? Tuple.Create(unit.RenderMapX.Value, unit.RenderMapY.Value)
                : Tuple.Create(unit.MapX, unit.MapY);
        }
    }
}
