using System;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Malco.Presentation.Hud
{
    internal sealed class HudWidgetVisualHandle
    {
        public HudWidgetVisualHandle(
            string key,
            Border root,
            Grid contentGrid,
            Border gameplayBodyHost,
            Border sampleHost,
            Border editorChrome,
            Thumb moveThumb,
            Thumb resizeThumb,
            Border resizeGrip,
            ScaleTransform visualScale)
        {
            Key = key ?? throw new ArgumentNullException(nameof(key));
            Root = root ?? throw new ArgumentNullException(nameof(root));
            ContentGrid = contentGrid ?? throw new ArgumentNullException(nameof(contentGrid));
            GameplayBodyHost = gameplayBodyHost ?? throw new ArgumentNullException(nameof(gameplayBodyHost));
            SampleHost = sampleHost ?? throw new ArgumentNullException(nameof(sampleHost));
            EditorChrome = editorChrome ?? throw new ArgumentNullException(nameof(editorChrome));
            MoveThumb = moveThumb ?? throw new ArgumentNullException(nameof(moveThumb));
            ResizeThumb = resizeThumb ?? throw new ArgumentNullException(nameof(resizeThumb));
            ResizeGrip = resizeGrip ?? throw new ArgumentNullException(nameof(resizeGrip));
            VisualScale = visualScale ?? throw new ArgumentNullException(nameof(visualScale));
        }

        public string Key { get; }

        public Border Root { get; }

        public Grid ContentGrid { get; }

        public Border GameplayBodyHost { get; }

        public Border SampleHost { get; }

        public Border EditorChrome { get; }

        public Thumb MoveThumb { get; }

        public Thumb ResizeThumb { get; }

        public Border ResizeGrip { get; }

        public ScaleTransform VisualScale { get; }

        public void SetGameplayContentAvailable(bool available, bool editorMode)
        {
            var showSample = editorMode && !available;
            GameplayBodyHost.Visibility = showSample
                ? System.Windows.Visibility.Collapsed
                : System.Windows.Visibility.Visible;
            SampleHost.Visibility = showSample
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;
        }
    }
}
