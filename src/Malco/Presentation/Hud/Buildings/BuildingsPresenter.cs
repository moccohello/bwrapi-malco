using System.Windows.Media;
using Malco.Presentation.Hud.Tiles;
using Malco.Presentation.Hud.Units;

namespace Malco.Presentation.Hud.Buildings
{
    internal sealed class BuildingsPresenter : UnitCountsPresenter
    {
        public BuildingsPresenter(HudTileFactory tileFactory, Brush mutedBrush)
            : base(tileFactory, mutedBrush, UnitHudContent.Buildings)
        {
        }
    }
}
