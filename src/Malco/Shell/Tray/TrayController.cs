using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Windows.Threading;
using Malco.Localization;

namespace Malco.Shell.Tray
{
    internal sealed class TrayController : IDisposable
    {
        private static string DefaultTooltip => UiText.Get("Malco");
        private static string HotkeyUnavailableTooltip => UiText.Get("Malco") + " - " + UiText.Get("hotkey unavailable");
        private static string ShutdownBlockedTooltip => UiText.Get("Malco") + " - " + UiText.Get("shutdown blocked");

        private readonly Dispatcher _dispatcher;
        private readonly ITrayIntentSink _intentSink;
        private readonly string _iconPath;
        private NativeTrayIcon _icon;
        private TrayMenuWindow _menu;
        private int _disposed;
        private int _resourcesDisposed;
        private bool _hotkeyUnavailable;
        private bool _windowEventFallback;
        private bool _shutdownBlocked;
        private string _shutdownDetail;

        public TrayController(Dispatcher dispatcher, ITrayIntentSink intentSink)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _intentSink = intentSink ?? throw new ArgumentNullException(nameof(intentSink));
            _iconPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "assets",
                "overlay-mutalisk.ico");

            _icon = new NativeTrayIcon(
                File.Exists(_iconPath) ? _iconPath : null,
                DefaultTooltip,
                OpenSettings,
                ShowMenu);
            _dispatcher.ShutdownStarted += OnDispatcherShutdownStarted;
        }

        public void RefreshLanguage()
        {
            RunOnDispatcher(() =>
            {
                RefreshTooltip();
                RefreshOpenMenu();
            });
        }

        public void ReportHotkeyUnavailable()
        {
            RunOnDispatcher(() =>
            {
                if (_hotkeyUnavailable)
                {
                    return;
                }

                _hotkeyUnavailable = true;
                RefreshTooltip();
                RefreshOpenMenu();
            });
        }

        public void ReportWindowEventFallback()
        {
            RunOnDispatcher(() =>
            {
                if (_windowEventFallback)
                {
                    return;
                }

                _windowEventFallback = true;
                RefreshOpenMenu();
            });
        }

        public void ReportShutdownBlocked(string message)
        {
            RunOnDispatcher(() =>
            {
                _shutdownBlocked = true;
                _shutdownDetail = string.IsNullOrWhiteSpace(message)
                    ? UiText.Get("Application/provider shutdown is blocked.")
                    : message.Trim();
                RefreshTooltip();
                RefreshOpenMenu();
            });
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            if (_dispatcher.CheckAccess())
            {
                DisposeResources();
            }
            else if (!_dispatcher.HasShutdownStarted && !_dispatcher.HasShutdownFinished)
            {
                _dispatcher.Invoke(DisposeResources);
            }
        }

        private bool IsDisposed => Volatile.Read(ref _disposed) != 0;

        private void OnDispatcherShutdownStarted(object sender, EventArgs args)
        {
            Interlocked.Exchange(ref _disposed, 1);
            DisposeResources();
        }

        private void DisposeResources()
        {
            if (Interlocked.Exchange(ref _resourcesDisposed, 1) != 0)
            {
                return;
            }

            _dispatcher.ShutdownStarted -= OnDispatcherShutdownStarted;
            CloseMenu();
            _icon?.Dispose();
            _icon = null;
        }

        private void OpenSettings()
        {
            if (IsDisposed)
            {
                return;
            }

            CloseMenu();
            _intentSink.OpenSettings();
        }

        private void RequestQuit()
        {
            if (IsDisposed)
            {
                return;
            }

            CloseMenu();
            _intentSink.RequestQuit();
        }

        private void ShowMenu(NativePoint cursorPosition)
        {
            if (IsDisposed)
            {
                return;
            }

            CloseMenu();
            var menu = new TrayMenuWindow(
                _iconPath,
                CreateMenuItems(),
                OpenSettings,
                RequestQuit);
            _menu = menu;
            menu.Closed += (_, _) =>
            {
                if (ReferenceEquals(_menu, menu))
                {
                    _menu = null;
                }
            };
            menu.ShowAt(cursorPosition.X, cursorPosition.Y);
        }

        private IReadOnlyList<string> CreateMenuItems()
        {
            var diagnostics = new List<string>(3);
            if (_hotkeyUnavailable)
            {
                diagnostics.Add(UiText.Get("Hotkey unavailable - use Settings"));
            }
            if (_windowEventFallback)
            {
                diagnostics.Add(UiText.Get("Window event fallback active"));
            }
            if (_shutdownBlocked)
            {
                diagnostics.Add(_shutdownDetail);
            }
            return diagnostics;
        }

        private void RefreshOpenMenu()
        {
            if (_menu == null)
            {
                return;
            }

            _menu.RefreshText(
                CreateMenuItems(),
                UiText.Get("Settings"),
                UiText.Get(_shutdownBlocked ? "Retry Quit" : "Quit Malco"));
        }

        private void RefreshTooltip()
        {
            _icon?.SetTooltip(_shutdownBlocked
                ? ShutdownBlockedTooltip
                : _hotkeyUnavailable
                    ? HotkeyUnavailableTooltip
                    : DefaultTooltip);
        }

        private void CloseMenu()
        {
            var menu = _menu;
            _menu = null;
            menu?.Dismiss();
        }

        private void RunOnDispatcher(Action action)
        {
            if (IsDisposed || _dispatcher.HasShutdownStarted || _dispatcher.HasShutdownFinished)
            {
                return;
            }

            if (_dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                _dispatcher.BeginInvoke(action);
            }
        }
    }
}
