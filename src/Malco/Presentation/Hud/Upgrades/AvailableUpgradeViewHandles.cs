using System;
using System.Windows.Controls;

namespace Malco.Presentation.Hud.Upgrades
{
    internal sealed class AvailableUpgradeViewHandles
    {
        public AvailableUpgradeViewHandles(Border panel, WrapPanel tiles)
        {
            Panel = panel ?? throw new ArgumentNullException(nameof(panel));
            Tiles = tiles ?? throw new ArgumentNullException(nameof(tiles));
        }

        public Border Panel { get; }
        public WrapPanel Tiles { get; }
    }
}
