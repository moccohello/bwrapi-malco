using Malco.Application.Contracts.Output;
using Malco.Data;
using Malco.Configuration.Models;

namespace Malco.Presentation.Spatial
{
    internal readonly struct SpatialFeaturePreferences
    {
        public SpatialFeaturePreferences(
            bool showBuildingRallyLines,
            bool showUnitCommandLines,
            bool showMineralWorkers,
            bool showGasWorkers,
            HudDisplayPreferences displayPreferences)
        {
            ShowBuildingRallyLines = showBuildingRallyLines;
            ShowUnitCommandLines = showUnitCommandLines;
            ShowMineralWorkers = showMineralWorkers;
            ShowGasWorkers = showGasWorkers;
            DisplayPreferences = displayPreferences;
        }

        public bool ShowBuildingRallyLines { get; }
        public bool ShowUnitCommandLines { get; }
        public bool ShowMineralWorkers { get; }
        public bool ShowGasWorkers { get; }
        public HudDisplayPreferences DisplayPreferences { get; }
    }

    internal readonly struct SpatialSurfaceState
    {
        public SpatialSurfaceState(
            bool isGameplay,
            bool isEditor,
            bool hasUsableTarget,
            double width,
            double height,
            bool originalAspectRatio)
        {
            IsGameplay = isGameplay;
            IsEditor = isEditor;
            HasUsableTarget = hasUsableTarget;
            Width = width;
            Height = height;
            OriginalAspectRatio = originalAspectRatio;
        }

        public bool IsGameplay { get; }
        public bool IsEditor { get; }
        public bool HasUsableTarget { get; }
        public double Width { get; }
        public double Height { get; }
        public bool OriginalAspectRatio { get; }
    }

    internal readonly struct SpatialSlowState
    {
        public SpatialSlowState(
            string sessionEpoch,
            long sessionGeneration,
            FrozenSemanticSnapshot snapshot,
            CommandProjectionState commands,
            SpatialFeaturePreferences preferences,
            SpatialSurfaceState surface,
            bool semanticDirty,
            bool commandDirty,
            long monotonicTimestamp)
        {
            SessionEpoch = sessionEpoch ?? string.Empty;
            SessionGeneration = sessionGeneration;
            Snapshot = snapshot;
            Commands = commands;
            Preferences = preferences;
            Surface = surface;
            SemanticDirty = semanticDirty;
            CommandDirty = commandDirty;
            MonotonicTimestamp = monotonicTimestamp;
        }

        public string SessionEpoch { get; }
        public long SessionGeneration { get; }
        public FrozenSemanticSnapshot Snapshot { get; }
        public CommandProjectionState Commands { get; }
        public SpatialFeaturePreferences Preferences { get; }
        public SpatialSurfaceState Surface { get; }
        public bool SemanticDirty { get; }
        public bool CommandDirty { get; }
        public long MonotonicTimestamp { get; }
    }

    internal readonly struct SpatialCompositionFrame
    {
        public SpatialCompositionFrame(
            bool isUsable,
            bool isAuthoritativeClear,
            string sessionEpoch,
            long sessionGeneration,
            long presentationRevision,
            int viewportMapX,
            int viewportMapY,
            SpatialSurfaceState surface)
        {
            IsUsable = isUsable;
            IsAuthoritativeClear = isAuthoritativeClear;
            SessionEpoch = sessionEpoch ?? string.Empty;
            SessionGeneration = sessionGeneration;
            PresentationRevision = presentationRevision;
            ViewportMapX = viewportMapX;
            ViewportMapY = viewportMapY;
            Surface = surface;
        }

        public bool IsUsable { get; }
        public bool IsAuthoritativeClear { get; }
        public string SessionEpoch { get; }
        public long SessionGeneration { get; }
        public long PresentationRevision { get; }
        public int ViewportMapX { get; }
        public int ViewportMapY { get; }
        public SpatialSurfaceState Surface { get; }
    }

    internal enum HudClipActionKind
    {
        Unchanged,
        Set,
        Clear
    }

    internal readonly struct HudClipAction
    {
        private HudClipAction(HudClipActionKind kind, System.Windows.Rect clip)
        {
            Kind = kind;
            Clip = clip;
        }

        public static HudClipAction Unchanged => new HudClipAction(HudClipActionKind.Unchanged, System.Windows.Rect.Empty);
        public static HudClipAction Clear => new HudClipAction(HudClipActionKind.Clear, System.Windows.Rect.Empty);
        public static HudClipAction Set(System.Windows.Rect clip) => new HudClipAction(HudClipActionKind.Set, clip);

        public HudClipActionKind Kind { get; }
        public System.Windows.Rect Clip { get; }
    }

    internal readonly struct SpatialFrameApplyResult
    {
        public SpatialFrameApplyResult(bool activePresentation, int positionWrites, HudClipAction hudClip)
        {
            ActivePresentation = activePresentation;
            PositionWrites = positionWrites;
            HudClip = hudClip;
        }

        public bool ActivePresentation { get; }
        public int PositionWrites { get; }
        public HudClipAction HudClip { get; }
    }

    internal readonly struct SpatialSlowApplyResult
    {
        public SpatialSlowApplyResult(
            bool structuralChanged,
            int creates,
            int updates,
            int removes,
            SpatialFrameApplyResult frame,
            bool frameInvalidated = false)
        {
            StructuralChanged = structuralChanged;
            Creates = creates;
            Updates = updates;
            Removes = removes;
            Frame = frame;
            FrameInvalidated = frameInvalidated;
        }

        public bool StructuralChanged { get; }
        public int Creates { get; }
        public int Updates { get; }
        public int Removes { get; }
        public SpatialFrameApplyResult Frame { get; }
        public bool FrameInvalidated { get; }
    }
}
