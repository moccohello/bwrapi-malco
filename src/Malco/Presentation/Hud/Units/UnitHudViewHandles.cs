using System;
using System.Windows.Controls;

namespace Malco.Presentation.Hud.Units
{
    internal sealed class UnitHudViewHandles
    {
        public UnitHudViewHandles(StackPanel body, WrapPanel tiles, TextBlock emptyText)
        {
            Body = body ?? throw new ArgumentNullException(nameof(body));
            Tiles = tiles ?? throw new ArgumentNullException(nameof(tiles));
            EmptyText = emptyText ?? throw new ArgumentNullException(nameof(emptyText));
        }

        public StackPanel Body { get; }

        public WrapPanel Tiles { get; }

        public TextBlock EmptyText { get; }
    }
}
