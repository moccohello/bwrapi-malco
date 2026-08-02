using System;

namespace Malco
{
    internal sealed class WidgetLayout
    {
        private const double DefaultCanvasWidth = 1280d;
        private const double DefaultCanvasHeight = 720d;

        public bool Enabled { get; set; }

        public double X { get; set; }

        public double Y { get; set; }

        public double Width { get; set; }

        public double Height { get; set; }

        public bool HasRelativeBounds { get; set; }

        public double XRatio { get; set; }

        public double YRatio { get; set; }

        public double WidthRatio { get; set; }

        public double HeightRatio { get; set; }

        public void Normalize(
            double x,
            double y,
            double width,
            double height,
            double minimumWidth,
            double minimumHeight)
        {
            var sizeChanged = false;
            if (Width < minimumWidth)
            {
                Width = Math.Max(minimumWidth, width);
                sizeChanged = true;
            }

            if (Height < minimumHeight)
            {
                Height = Math.Max(minimumHeight, height);
                sizeChanged = true;
            }

            if (X < 0d)
            {
                X = x;
            }

            if (Y < 0d)
            {
                Y = y;
            }

            if (!HasRelativeBounds)
            {
                UpdateRelativeBounds(DefaultCanvasWidth, DefaultCanvasHeight);
            }
            else if (sizeChanged)
            {
                UpdateRelativeSize(DefaultCanvasWidth, DefaultCanvasHeight);
            }
        }

        public void ApplyRelativePosition(double canvasWidth, double canvasHeight)
        {
            if (!HasRelativeBounds || canvasWidth <= 0d || canvasHeight <= 0d)
            {
                return;
            }

            X = XRatio * canvasWidth;
            Y = YRatio * canvasHeight;
        }

        public void UpdateRelativeBounds(double canvasWidth, double canvasHeight)
        {
            if (canvasWidth <= 0d || canvasHeight <= 0d)
            {
                return;
            }

            XRatio = ClampRatio(X / canvasWidth);
            YRatio = ClampRatio(Y / canvasHeight);
            WidthRatio = ClampRatio(Width / canvasWidth);
            HeightRatio = ClampRatio(Height / canvasHeight);
            HasRelativeBounds = true;
        }

        public void UpdateRelativePosition(double canvasWidth, double canvasHeight)
        {
            if (canvasWidth <= 0d || canvasHeight <= 0d)
            {
                return;
            }

            XRatio = ClampRatio(X / canvasWidth);
            YRatio = ClampRatio(Y / canvasHeight);
            HasRelativeBounds = true;
        }

        public void UpdateRelativeSize(double canvasWidth, double canvasHeight)
        {
            if (canvasWidth <= 0d || canvasHeight <= 0d)
            {
                return;
            }

            WidthRatio = ClampRatio(Width / canvasWidth);
            HeightRatio = ClampRatio(Height / canvasHeight);
            HasRelativeBounds = true;
        }

        private static double ClampRatio(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                return 0d;
            }

            if (value < 0d)
            {
                return 0d;
            }

            return value > 1d ? 1d : value;
        }
    }
}
