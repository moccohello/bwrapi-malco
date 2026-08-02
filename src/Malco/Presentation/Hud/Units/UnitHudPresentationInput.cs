using Malco.Configuration.Models;
using Malco.Data;

namespace Malco.Presentation.Hud.Units
{
    internal readonly struct UnitHudPresentationInput
    {
        public UnitHudPresentationInput(
            FrozenSemanticSnapshot snapshot,
            long sessionGeneration,
            HudDisplayPreferences preferences,
            bool editorMode)
        {
            Snapshot = snapshot;
            SessionGeneration = sessionGeneration;
            Preferences = preferences;
            EditorMode = editorMode;
        }

        public FrozenSemanticSnapshot Snapshot { get; }

        public long SessionGeneration { get; }

        public HudDisplayPreferences Preferences { get; }

        public bool EditorMode { get; }
    }
}
