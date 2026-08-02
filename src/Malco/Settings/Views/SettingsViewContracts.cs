using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Malco.Models;
using Malco.Settings.Contracts;
using Malco.Settings.Controller;

namespace Malco.Settings.Views
{
    internal static class SettingsVisualTokens
    {
        public const string Backdrop = "#CC05080B";
        public const string Panel = "#FF0C131A";
        public const string Surface = "#FF111A22";
        public const string RaisedSurface = "#FF17232D";
        public const string HoverSurface = "#FF1D2B36";
        public const string SelectedSurface = "#FF17343B";
        public const string Separator = "#FF26343F";
        public const string ControlBorder = "#FF586A76";
        public const string TextPrimary = "#FFF4F7F9";
        public const string TextSecondary = "#FFADB9C2";
        public const string Accent = "#FF55C8D8";
        public const string AccentHover = "#FF72D4E0";
        public const string AccentPressed = "#FF34A8B8";
        public const string FocusRing = "#FF92E0E8";
        public const string OnAccent = "#FF071417";
        public const string Warning = "#FFF0C75E";
        public const string WarningHover = "#FFF6D77E";
        public const string WarningPressed = "#FFD5A83E";
        public const string WarningSurface = "#FF2A2415";
        public const string Error = "#FFFF7B72";
        public const string ErrorSurface = "#FF2C191B";
        public const string ToggleOff = "#FF2B3943";
        public const string ChipBackground = "#E00B1218";
        public const string ChipBorder = "#B03A4B57";
    }

    internal sealed class SettingsViewPalette
    {
        public Brush TextBrush { get; init; }
        public Brush MutedBrush { get; init; }
        public Brush InkBrush { get; init; }
        public Brush AccentBrush { get; init; }
        public Brush FocusBrush { get; init; }
        public Brush WarningBrush { get; init; }
        public Brush WarningSurfaceBrush { get; init; }
        public Brush DangerBrush { get; init; }
        public Brush DangerSurfaceBrush { get; init; }
        public Brush ChipBackgroundBrush { get; init; }
        public Brush ChipBorderBrush { get; init; }
        public Brush PanelBrush { get; init; }
        public Brush SurfaceBrush { get; init; }
        public Brush RaisedSurfaceBrush { get; init; }
        public Brush HoverSurfaceBrush { get; init; }
        public Brush SelectedSurfaceBrush { get; init; }
        public Brush BorderBrush { get; init; }
        public Brush SeparatorBrush { get; init; }
        public Func<string, double, FontWeight, Brush, TextBlock> Text { get; init; }
        public Func<ImageSource, ImageSource> GrayscaleIcon { get; init; }
    }

    internal interface ISettingsViewActions
    {
        bool EditorMode { get; }
        SettingsPage ActiveEditorPage { get; set; }
        string SelectedWidgetKey { get; set; }
        Race SelectedTechTreeRace { get; set; }
        double ViewWidth { get; }
        Dispatcher Dispatcher { get; }
        HudLayoutConfig Layout { get; }
        bool HudTemporarilyHidden { get; }
        string ProgramVersion { get; }
        SettingsEditResult ApplyEdit(SettingsEdit edit);
        void Dispatch(SettingsIntent intent);
        bool IsFeatureEnabled(string key);
        void SetWidgetEnabled(string key, bool enabled);
        void SelectWidget(string key);
        void ResetWidgetLayout(string key);
        void ResetAllWidgetLayouts();
        void RetrySettingsSave();
        void RefreshPresenterViews();
        void RefreshSpatialPresentation();
        void RefreshVisibility();
        void UpdateEditorPlacement();
        void RefreshEditorView();
        void FocusActiveEditorSurface();
        void SetHudTemporarilyHidden(bool hidden);
    }

    internal interface ISettingsEditorChrome
    {
        Button ActionButton(string label);
        void ConfigureScrollViewer(ScrollViewer scroll);
        Style ButtonStyle();
    }
}
