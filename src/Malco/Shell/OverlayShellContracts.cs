using System;
using System.Windows;
using System.Windows.Threading;

namespace Malco.Shell
{
    internal enum OverlayRuntimeMode
    {
        SettingsOnly,
        Gameplay,
        Editor
    }

    internal interface IOverlayShellViewPort
    {
        Dispatcher Dispatcher { get; }
        bool IsOverlayVisible { get; }
        bool IsOverlayTopmost { get; set; }
        double OverlayLeft { get; }
        double OverlayTop { get; }
        double OverlayWidth { get; }
        double OverlayHeight { get; }
        void ShowOverlay();
        void HideOverlay();
        void ApplyShellBounds(Rect bounds, bool clampWidgets);
        void PositionSettingsButtonAtOrigin();
    }

    internal interface ISettingsShellPort
    {
        bool EditorMode { get; }
        bool ShutdownRequested { get; }
        bool ResourcesDisposed { get; }
        void RequestApplicationShutdown();
        void SetShellStatus(string status);
        void SetShellHelpText(string message);
        void ReportWindowEventFallback();
        void ReportHotkeyUnavailable();
    }

    internal interface IShellPresentationPort
    {
        OverlayRuntimeMode DesiredRuntimeMode { get; }
        void ApplyRuntimeVisualState(OverlayRuntimeMode mode, bool originalAspectRatio);
        void ResetForMissingTarget(string message);
        void InvalidateSpatialSurface();
    }
}
