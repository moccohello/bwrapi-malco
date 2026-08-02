using System;
using System.Windows;

namespace Malco.Presentation.Spatial
{
    internal static class ProjectionCalculator
    {
        private const double OriginalLogicalWidth = 640d;
        private const double OriginalAspectRatio = 4d / 3d;
        private const double WidescreenAspectRatio = 16d / 9d;
        private const double LogicalHeight = 480d;
        private const double CommandUiTop = 352d;
        private const double HudReferenceWidth = 1280d;
        private const double HudReferenceHeight = 720d;
        private const double HudMinimumScale = 0.5d;
        private const double HudMaximumScale = 2d;

        public static bool TryCreateGameRenderFrame(SpatialSurfaceState surface, out GameRenderFrame frame)
        {
            frame = default;
            if (surface.Width <= 0d || surface.Height <= 0d) return false;

            var clientAspectRatio = surface.Width / surface.Height;
            var renderAspectRatio = surface.OriginalAspectRatio
                ? OriginalAspectRatio
                : Math.Max(OriginalAspectRatio, Math.Min(WidescreenAspectRatio, clientAspectRatio));
            var logicalWidth = surface.OriginalAspectRatio
                ? OriginalLogicalWidth
                : LogicalHeight * renderAspectRatio;
            var scale = Math.Min(surface.Width / logicalWidth, surface.Height / LogicalHeight);
            if (double.IsNaN(scale) || double.IsInfinity(scale) || scale <= 0d) return false;

            var renderWidth = logicalWidth * scale;
            var renderHeight = LogicalHeight * scale;
            var originX = Math.Max(0d, (surface.Width - renderWidth) / 2d);
            var originY = Math.Max(0d, (surface.Height - renderHeight) / 2d);
            frame = new GameRenderFrame(
                scale,
                new Rect(
                    originX,
                    originY,
                    renderWidth,
                    CommandUiTop * scale));
            return true;
        }

        public static bool TryCreateProjection(
            bool isUsable,
            int viewportMapX,
            int viewportMapY,
            SpatialSurfaceState surface,
            out SpatialProjection projection,
            out GameRenderFrame renderFrame)
        {
            projection = default;
            renderFrame = default;
            if (!isUsable ||
                !TryCreateGameRenderFrame(surface, out renderFrame)) return false;

            var rect = renderFrame.GameplayRect;
            projection = new SpatialProjection(
                viewportMapX,
                viewportMapY,
                renderFrame.Scale,
                GetHudUiScale(surface.Width, surface.Height),
                rect.Left,
                rect.Top,
                rect.Right,
                rect.Bottom);
            return true;
        }

        public static Rect BuildHudGameplayClip(SpatialSurfaceState surface, GameRenderFrame renderFrame)
        {
            return new Rect(
                0d,
                0d,
                Math.Max(0d, surface.Width),
                Math.Max(0d, Math.Min(surface.Height, renderFrame.GameplayRect.Bottom)));
        }

        public static double GetHudUiScale(double width, double height)
        {
            if (width <= 0d || height <= 0d) return 1d;
            return Math.Max(
                HudMinimumScale,
                Math.Min(HudMaximumScale, Math.Min(width / HudReferenceWidth, height / HudReferenceHeight)));
        }
    }
}
