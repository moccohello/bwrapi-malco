using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Malco.Presentation.Hud
{
    internal sealed class HudVisualTree
    {
        private readonly Canvas _host;
        private readonly Dictionary<string, HudWidgetVisualHandle> _widgetsByKey =
            new Dictionary<string, HudWidgetVisualHandle>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _widgetsWithoutGameplayContent =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private Rect _clipStamp = Rect.Empty;

        public HudVisualTree(Canvas host)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
        }

        public bool TryGet(string key, out HudWidgetVisualHandle handle) =>
            _widgetsByKey.TryGetValue(key, out handle);

        public void Attach(HudWidgetVisualHandle handle)
        {
            if (handle == null) throw new ArgumentNullException(nameof(handle));
            _widgetsByKey.Add(handle.Key, handle);
            _widgetsWithoutGameplayContent.Add(handle.Key);
            _host.Children.Add(handle.Root);
        }

        public void SetGameplayContentAvailable(string key, bool available, bool editorMode)
        {
            HudWidgetVisualHandle handle;
            if (!_widgetsByKey.TryGetValue(key, out handle)) return;

            if (available)
            {
                _widgetsWithoutGameplayContent.Remove(key);
            }
            else
            {
                _widgetsWithoutGameplayContent.Add(key);
            }

            handle.SetGameplayContentAvailable(available, editorMode);
        }

        public bool HasGameplayContent(string key) =>
            _widgetsByKey.ContainsKey(key) && !_widgetsWithoutGameplayContent.Contains(key);

        public void SetClip(Rect clipRect)
        {
            if (_host.Clip != null && !_clipStamp.IsEmpty && AreClose(_clipStamp, clipRect)) return;
            _host.Clip = new RectangleGeometry(clipRect);
            _clipStamp = clipRect;
        }

        public void ClearClip()
        {
            if (_host.Clip == null && _clipStamp.IsEmpty) return;
            _host.Clip = null;
            _clipStamp = Rect.Empty;
        }

        private static bool AreClose(Rect left, Rect right)
        {
            return Math.Abs(left.X - right.X) <= .5d &&
                   Math.Abs(left.Y - right.Y) <= .5d &&
                   Math.Abs(left.Width - right.Width) <= .5d &&
                   Math.Abs(left.Height - right.Height) <= .5d;
        }
    }
}
