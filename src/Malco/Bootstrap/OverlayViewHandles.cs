using System;
using System.Windows.Controls;
using System.Windows.Media;

namespace Malco.Bootstrap
{
    internal sealed class OverlayViewHandles
    {
        public OverlayViewHandles(
            Canvas spatialCanvas,
            Canvas hudCanvas,
            Brush textBrush,
            Brush mutedBrush,
            Brush amberBrush,
            Brush coralBrush,
            Brush chipBackgroundBrush,
            Brush chipBorderBrush,
            Func<ImageSource, ImageSource> grayscaleIcon)
        {
            SpatialCanvas = spatialCanvas;
            HudCanvas = hudCanvas;
            TextBrush = textBrush;
            MutedBrush = mutedBrush;
            AmberBrush = amberBrush;
            CoralBrush = coralBrush;
            ChipBackgroundBrush = chipBackgroundBrush;
            ChipBorderBrush = chipBorderBrush;
            GrayscaleIcon = grayscaleIcon;
        }

        public Canvas SpatialCanvas { get; }
        public Canvas HudCanvas { get; }
        public Brush TextBrush { get; }
        public Brush MutedBrush { get; }
        public Brush AmberBrush { get; }
        public Brush CoralBrush { get; }
        public Brush ChipBackgroundBrush { get; }
        public Brush ChipBorderBrush { get; }
        public Func<ImageSource, ImageSource> GrayscaleIcon { get; }
    }
}
