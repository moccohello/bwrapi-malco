using Malco.Data;
using Malco.Configuration.Models;
using Malco.Models;
using Malco.Presentation.Spatial;
using Malco.Shell;

namespace Malco.Presentation
{
    internal interface IOverlaySceneViewPort
    {
        bool ShutdownRequested { get; }
        bool EditorMode { get; }
        HudDisplayPreferences DisplayPreferences { get; }
        bool IsFeatureEnabled(string key);
        bool HasWidgetGameplayContent(string key);
        void SetWidgetGameplayContent(string key, bool content);
        void UpdateSettingsButtonStatus(string message, FrozenSemanticSnapshot snapshot);
        void RecordSpatialResult(SpatialSlowApplyResult result);
        SpatialSurfaceState BuildSpatialSurfaceState(bool originalAspectRatio);
        void RefreshVisibility();
    }
}
