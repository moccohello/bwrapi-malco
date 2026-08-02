using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Malco.Presentation.Hud;

namespace Malco.Settings.Views
{
    internal sealed partial class HudWidgetView
    {
        private const double HudReferenceWidth = 1280d;
        private const double HudReferenceHeight = 720d;
        private static readonly Brush EditorAccentBrush = FrozenBrush(SettingsVisualTokens.Accent);
        private static readonly Brush EditorBorderBrush = FrozenBrush(SettingsVisualTokens.ControlBorder);
        private static readonly Brush EditorChromeBrush = FrozenBrush(SettingsVisualTokens.Panel);
        private static readonly Brush EditorSelectedSurfaceBrush = FrozenBrush(SettingsVisualTokens.SelectedSurface, 178);
        private static readonly Brush EditorSurfaceBrush = FrozenBrush(SettingsVisualTokens.Panel, 96);
        private readonly double _minWidth;
        private readonly double _minHeight;
        private readonly Brush _textBrush;
        private bool _editorMode;
        private double _scale = 1d;
        private Canvas _resizeCanvas;
        private Point _resizeStartPointer;
        private double _resizeStartWidth;
        private double _resizeStartHeight;
        private double _resizeStartScale = 1d;
        private bool _resizeActive;
        private bool _pointerOver;
        private bool _updatingTileVisibility;
    
        public HudWidgetView(string key, string title, WidgetLayout layout, UIElement body, UIElement sampleBody, Brush textBrush)
        {
            Key = key;
            Title = title;
            Layout = layout;
            _minWidth = HudWidgetLayoutPolicy.MinimumWidth(key);
            _minHeight = HudWidgetLayoutPolicy.MinimumHeight(key);
            _textBrush = textBrush ?? Brushes.White;
            var visualScale = new ScaleTransform(1d, 1d);
            var root = new Border
            {
                Width = layout.Width,
                Height = layout.Height,
                CornerRadius = new CornerRadius(8d),
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0d),
                Background = Brushes.Transparent,
                RenderTransformOrigin = new Point(0d, 0d),
                RenderTransform = visualScale
            };
    
            var contentGrid = new Grid
            {
                ClipToBounds = true
            };
            contentGrid.LayoutUpdated += OnContentLayoutUpdated;
            var bodyHost = new Border
            {
                Child = WrapScalableContent(key, body)
            };
            contentGrid.Children.Add(bodyHost);
            var sampleHost = new Border
            {
                Child = WrapScalableContent(key, sampleBody),
                Visibility = Visibility.Collapsed
            };
            contentGrid.Children.Add(sampleHost);
            var editorChrome = BuildEditorChrome(title);
            contentGrid.Children.Add(editorChrome);
    
            var moveThumb = new Thumb
            {
                Cursor = Cursors.SizeAll,
                Opacity = 0d,
                Background = Brushes.Transparent,
                Focusable = true
            };
            moveThumb.DragDelta += OnMove;
            Grid.SetRowSpan(moveThumb, 2);
            Panel.SetZIndex(moveThumb, 11);
            contentGrid.Children.Add(moveThumb);
    
            var resizeGrip = new Border
            {
                Width = 12d,
                Height = 12d,
                CornerRadius = new CornerRadius(3d),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Background = EditorChromeBrush,
                BorderThickness = new Thickness(0d),
                Margin = new Thickness(0d, 0d, 2d, 2d),
                Child = new Path
                {
                    Data = Geometry.Parse("M 1,8 L 8,1 M 4,8 L 8,4"),
                    Stroke = EditorAccentBrush,
                    StrokeThickness = 1.25d,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round,
                    Stretch = Stretch.Uniform
                },
                IsHitTestVisible = false,
                Visibility = Visibility.Collapsed
            };
            Panel.SetZIndex(resizeGrip, 12);
            contentGrid.Children.Add(resizeGrip);
    
            var resizeThumb = new Thumb
            {
                Width = 18d,
                Height = 18d,
                Cursor = Cursors.SizeNWSE,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Template = CreateResizeThumbTemplate(),
                Margin = new Thickness(0d, 0d, 2d, 2d),
                Focusable = false
            };
            resizeThumb.DragStarted += OnResizeStarted;
            resizeThumb.DragDelta += OnResize;
            resizeThumb.DragCompleted += (sender, args) =>
            {
                _resizeActive = false;
                _resizeCanvas = null;
            };
            Panel.SetZIndex(resizeThumb, 13);
            contentGrid.Children.Add(resizeThumb);
    
            root.Child = contentGrid;
            Handle = new HudWidgetVisualHandle(
                key,
                root,
                contentGrid,
                bodyHost,
                sampleHost,
                editorChrome,
                moveThumb,
                resizeThumb,
                resizeGrip,
                visualScale);
            Root.ToolTip = title;
            Root.PreviewMouseLeftButtonDown += OnSelect;
            Root.GotKeyboardFocus += OnKeyboardFocus;
            Root.PreviewKeyDown += OnEditorKeyDown;
            Root.MouseEnter += (sender, args) =>
            {
                _pointerOver = true;
                RefreshEditorChromeVisibility();
            };
            Root.MouseLeave += (sender, args) =>
            {
                _pointerOver = false;
                RefreshEditorChromeVisibility();
            };
        }

        private static ControlTemplate CreateResizeThumbTemplate()
        {
            var visual = new FrameworkElementFactory(typeof(Border));
            visual.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            visual.SetValue(Border.CornerRadiusProperty, new CornerRadius(4d));
            return new ControlTemplate(typeof(Thumb))
            {
                VisualTree = visual
            };
        }

        private static UIElement WrapScalableContent(string key, UIElement content)
        {
            if (!string.Equals(key, HudWidgetRegistry.Workers, StringComparison.OrdinalIgnoreCase))
            {
                return content;
            }

            return new Viewbox
            {
                Child = content,
                Stretch = Stretch.Uniform,
                StretchDirection = StretchDirection.Both,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
        }
    
        public event EventHandler Selected;
    
        public event EventHandler LayoutChanged;
    
        public string Key { get; private set; }
    
        public string Title { get; private set; }
    
        public WidgetLayout Layout { get; private set; }
    
        public HudWidgetVisualHandle Handle { get; private set; }
    
        public Border Root
        {
            get { return Handle.Root; }
        }
    
        public bool IsKeyboardFocusWithin
        {
            get { return Root.IsKeyboardFocusWithin; }
        }

        public void SetTitle(string title)
        {
            Title = title ?? string.Empty;
        }

        public void SetSampleBody(UIElement sampleBody)
        {
            Handle.SampleHost.Child = WrapScalableContent(Key, sampleBody);
        }
    
        private Border BuildEditorChrome(string title)
        {
            return new Border
            {
                Height = 20d,
                MaxWidth = 180d,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(4d),
                Background = EditorChromeBrush,
                BorderBrush = EditorBorderBrush,
                BorderThickness = new Thickness(1d),
                CornerRadius = new CornerRadius(4d),
                Padding = new Thickness(6d, 1d, 6d, 1d),
                IsHitTestVisible = false,
                Visibility = Visibility.Collapsed,
                Child = new TextBlock
                {
                    Text = title,
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 10d,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = _textBrush,
                    TextWrapping = TextWrapping.NoWrap,
                    TextTrimming = TextTrimming.CharacterEllipsis
                }
            };
        }

        private static SolidColorBrush FrozenBrush(string color, byte? alpha = null)
        {
            var parsed = (Color)ColorConverter.ConvertFromString(color);
            if (alpha.HasValue)
            {
                parsed = Color.FromArgb(alpha.Value, parsed.R, parsed.G, parsed.B);
            }
            var brush = new SolidColorBrush(parsed);
            brush.Freeze();
            return brush;
        }
    
    }
}
