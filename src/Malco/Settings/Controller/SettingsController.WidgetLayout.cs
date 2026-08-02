using System;
using Malco.Settings.Contracts;

namespace Malco.Settings.Controller
{
    internal sealed partial class SettingsController
    {
        private bool SetWidgetEnabled(string key, bool enabled)
        {
            var definition = FindWidgetDefinition(key);
            if (definition == null)
            {
                return false;
            }

            WidgetLayout widget;
            if (_layout.Widgets == null ||
                !_layout.Widgets.TryGetValue(definition.Key, out widget) ||
                widget == null)
            {
                if (definition.EnabledByDefault == enabled)
                {
                    return false;
                }
                widget = _layout.GetOrCreate(
                    definition.Key,
                    definition.X,
                    definition.Y,
                    definition.Width,
                    definition.Height,
                    definition.EnabledByDefault);
            }

            if (widget.Enabled == enabled)
            {
                return false;
            }

            widget.Enabled = enabled;
            return true;
        }

        private bool SetWidgetBounds(string key, WidgetBoundsValue bounds)
        {
            var definition = FindWidgetDefinition(key);
            if (definition == null)
            {
                return false;
            }

            WidgetLayout widget;
            if (_layout.Widgets == null ||
                !_layout.Widgets.TryGetValue(definition.Key, out widget) ||
                widget == null)
            {
                widget = _layout.GetOrCreate(
                    definition.Key,
                    definition.X,
                    definition.Y,
                    definition.Width,
                    definition.Height,
                    definition.EnabledByDefault);
            }

            var minimumWidth = HudWidgetLayoutPolicy.MinimumWidth(definition.Key);
            var minimumHeight = HudWidgetLayoutPolicy.MinimumHeight(definition.Key);
            var fallbackX = IsValidCoordinate(widget.X)
                ? widget.X
                : Math.Max(0d, definition.X);
            var fallbackY = IsValidCoordinate(widget.Y)
                ? widget.Y
                : Math.Max(0d, definition.Y);
            var fallbackWidth = IsValidDimension(widget.Width, minimumWidth)
                ? widget.Width
                : Math.Max(minimumWidth, definition.Width);
            var fallbackHeight = IsValidDimension(widget.Height, minimumHeight)
                ? widget.Height
                : Math.Max(minimumHeight, definition.Height);
            var x = IsValidCoordinate(bounds.X) ? bounds.X : fallbackX;
            var y = IsValidCoordinate(bounds.Y) ? bounds.Y : fallbackY;
            var width = IsValidDimension(bounds.Width, minimumWidth) ? bounds.Width : fallbackWidth;
            var height = IsValidDimension(bounds.Height, minimumHeight) ? bounds.Height : fallbackHeight;
            var xRatio = SanitizeRatio(
                bounds.XRatio,
                widget.HasRelativeBounds && IsFinite(widget.XRatio) ? widget.XRatio : x / DefaultCanvasWidth);
            var yRatio = SanitizeRatio(
                bounds.YRatio,
                widget.HasRelativeBounds && IsFinite(widget.YRatio) ? widget.YRatio : y / DefaultCanvasHeight);
            var widthRatio = SanitizeRatio(
                bounds.WidthRatio,
                widget.HasRelativeBounds && IsFinite(widget.WidthRatio) ? widget.WidthRatio : width / DefaultCanvasWidth);
            var heightRatio = SanitizeRatio(
                bounds.HeightRatio,
                widget.HasRelativeBounds && IsFinite(widget.HeightRatio) ? widget.HeightRatio : height / DefaultCanvasHeight);

            if (widget.X == x &&
                widget.Y == y &&
                widget.Width == width &&
                widget.Height == height &&
                widget.HasRelativeBounds == bounds.HasRelativeBounds &&
                widget.XRatio == xRatio &&
                widget.YRatio == yRatio &&
                widget.WidthRatio == widthRatio &&
                widget.HeightRatio == heightRatio)
            {
                return false;
            }

            widget.X = x;
            widget.Y = y;
            widget.Width = width;
            widget.Height = height;
            widget.HasRelativeBounds = bounds.HasRelativeBounds;
            widget.XRatio = xRatio;
            widget.YRatio = yRatio;
            widget.WidthRatio = widthRatio;
            widget.HeightRatio = heightRatio;
            return true;
        }

        private static HudWidgetDefinition FindWidgetDefinition(string key)
        {
            foreach (var candidate in HudWidgetRegistry.EditorFeatures())
            {
                if (string.Equals(candidate.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }

            return null;
        }

        private bool ResetWidgetBounds(string key)
        {
            var definition = FindWidgetDefinition(key);
            if (definition == null ||
                HudWidgetRegistry.IsSpatialFeature(key))
            {
                return false;
            }

            return SetWidgetBounds(
                key,
                new WidgetBoundsValue(
                    definition.X,
                    definition.Y,
                    definition.Width,
                    definition.Height,
                    true,
                    definition.X / DefaultCanvasWidth,
                    definition.Y / DefaultCanvasHeight,
                    definition.Width / DefaultCanvasWidth,
                    definition.Height / DefaultCanvasHeight));
        }

        private bool ResetAllWidgetBounds()
        {
            var changed = false;
            foreach (var definition in HudWidgetRegistry.EditorFeatures())
            {
                changed |= ResetWidgetBounds(definition.Key);
            }
            return changed;
        }

        private static bool IsValidCoordinate(double value)
        {
            return IsFinite(value) && value >= 0d;
        }

        private static bool IsValidDimension(double value, double minimum)
        {
            return IsFinite(value) && value >= minimum;
        }

        private static double SanitizeRatio(double value, double fallback)
        {
            return ClampRatio(IsFinite(value) ? value : fallback);
        }

        private static double ClampRatio(double value)
        {
            if (!IsFinite(value) || value <= 0d)
            {
                return 0d;
            }

            return value >= 1d ? 1d : value;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
