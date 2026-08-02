using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Malco.Presentation.Hud.Tiles;

namespace Malco.Settings.Views
{
    internal sealed partial class HudWidgetView
    {
        public void ApplyBounds(Canvas canvas, double scale)
        {
            ApplyBounds(canvas, false, true, scale);
        }

        public void ApplyVisualScale(double scale)
        {
            _scale = double.IsNaN(scale) || double.IsInfinity(scale) || scale <= 0d ? 1d : scale;
            Handle.VisualScale.ScaleX = _scale;
            Handle.VisualScale.ScaleY = _scale;
            var logicalHandleSize = 18d / _scale;
            Handle.ResizeThumb.Width = logicalHandleSize;
            Handle.ResizeThumb.Height = logicalHandleSize;
            var logicalInset = 2d / _scale;
            Handle.ResizeThumb.Margin = new Thickness(0d, 0d, logicalInset, logicalInset);
            var logicalGripSize = 12d / _scale;
            Handle.ResizeGrip.Width = logicalGripSize;
            Handle.ResizeGrip.Height = logicalGripSize;
            Handle.ResizeGrip.CornerRadius = new CornerRadius(3d / _scale);
            var gripPath = Handle.ResizeGrip.Child as Path;
            if (gripPath != null)
            {
                gripPath.StrokeThickness = 1.25d / _scale;
            }
            var logicalGripInset = 2d / _scale;
            Handle.ResizeGrip.Margin = new Thickness(0d, 0d, logicalGripInset, logicalGripInset);
        }

        public void ApplyBounds(Canvas canvas, bool updateRelativeBounds, bool applyRelativeBounds)
        {
            ApplyBounds(canvas, updateRelativeBounds, applyRelativeBounds, _scale);
        }

        private void ApplyBounds(Canvas canvas, bool updateRelativeBounds, bool applyRelativeBounds, double scale)
        {
            ApplyVisualScale(scale);
            var actualWidth = canvas.ActualWidth > 0d ? canvas.ActualWidth : HudReferenceWidth;
            var actualHeight = canvas.ActualHeight > 0d ? canvas.ActualHeight : HudReferenceHeight;
            var maxWidth = Math.Max(_minWidth, (actualWidth - 16d) / _scale);
            var maxHeight = Math.Max(_minHeight, (actualHeight - 16d) / _scale);
            var canvasWidth = Math.Max(16d, actualWidth);
            var canvasHeight = Math.Max(16d, actualHeight);
            if (applyRelativeBounds)
            {
                Layout.ApplyRelativePosition(canvasWidth, canvasHeight);
            }

            var effectiveWidth = Math.Min(Math.Max(_minWidth, Layout.Width), maxWidth);
            var effectiveHeight = Math.Min(Math.Max(_minHeight, Layout.Height), maxHeight);
            if (updateRelativeBounds)
            {
                Layout.Width = effectiveWidth;
                Layout.Height = effectiveHeight;
            }
            Layout.X = Math.Min(
                Math.Max(8d, Layout.X),
                Math.Max(8d, canvasWidth - effectiveWidth * _scale - 8d));
            Layout.Y = Math.Min(
                Math.Max(8d, Layout.Y),
                Math.Max(8d, canvasHeight - effectiveHeight * _scale - 8d));
            if (updateRelativeBounds)
            {
                Layout.UpdateRelativeBounds(canvasWidth, canvasHeight);
            }

            Root.Width = effectiveWidth;
            Root.Height = effectiveHeight;
            Canvas.SetLeft(Root, Layout.X);
            Canvas.SetTop(Root, Layout.Y);
        }

        private void OnContentLayoutUpdated(object sender, EventArgs args)
        {
            if (_updatingTileVisibility || Handle == null ||
                Handle.ContentGrid.ActualWidth <= 0d || Handle.ContentGrid.ActualHeight <= 0d)
            {
                return;
            }

            _updatingTileVisibility = true;
            try
            {
                var host = Handle.SampleHost.Visibility == Visibility.Visible
                    ? (DependencyObject)Handle.SampleHost
                    : Handle.GameplayBodyHost;
                UpdateWholeTileVisibility(host, Handle.ContentGrid);
            }
            finally
            {
                _updatingTileVisibility = false;
            }
        }

        private static void UpdateWholeTileVisibility(DependencyObject parent, FrameworkElement viewport)
        {
            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
            {
                var child = VisualTreeHelper.GetChild(parent, index);
                if (child is FrameworkElement element &&
                    HudTileFactory.GetHideWhenClipped(element))
                {
                    var bounds = element.TransformToAncestor(viewport)
                        .TransformBounds(new Rect(new Point(0d, 0d), element.RenderSize));
                    var fullyVisible = bounds.Left >= 0d && bounds.Top >= 0d &&
                                       bounds.Right <= viewport.ActualWidth &&
                                       bounds.Bottom <= viewport.ActualHeight;
                    element.Visibility = fullyVisible ? Visibility.Visible : Visibility.Hidden;
                }
                else
                {
                    UpdateWholeTileVisibility(child, viewport);
                }
            }
        }
        private void ApplyPositionBounds(Canvas canvas)
        {
            ApplyBounds(canvas, false, false);
            var canvasWidth = canvas.ActualWidth > 0d ? canvas.ActualWidth : HudReferenceWidth;
            var canvasHeight = canvas.ActualHeight > 0d ? canvas.ActualHeight : HudReferenceHeight;
            Layout.UpdateRelativePosition(canvasWidth, canvasHeight);
        }
    }
}
