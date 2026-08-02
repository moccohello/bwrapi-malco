using System.Windows;

namespace Malco.Presentation.Spatial
{
    internal readonly struct GameRenderFrame
    {
        public GameRenderFrame(double scale, Rect gameplayRect)
        {
            Scale = scale;
            GameplayRect = gameplayRect;
        }

        public double Scale { get; }
        public Rect GameplayRect { get; }
    }
}
