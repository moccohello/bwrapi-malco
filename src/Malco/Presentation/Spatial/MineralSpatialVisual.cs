using System.Windows.Controls;
using Malco.Models;

namespace Malco.Presentation.Spatial
{
    internal sealed class MineralSpatialVisual
    {
        public MineralSpatialVisual(MineralWorkerGroup group, Border badge, TextBlock text)
        {
            Group = group;
            Badge = badge;
            Text = text;
        }

        public MineralWorkerGroup Group { get; set; }

        public Border Badge { get; private set; }

        public TextBlock Text { get; private set; }
    }
}
