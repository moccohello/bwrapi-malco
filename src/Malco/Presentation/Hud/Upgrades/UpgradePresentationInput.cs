using Malco.Configuration.Models;
using Malco.Data;

namespace Malco.Presentation.Hud.Upgrades
{
    internal readonly struct UpgradePresentationInput
    {
        public UpgradePresentationInput(FrozenSemanticSnapshot snapshot, long sessionGeneration, HudDisplayPreferences preferences, bool editorMode)
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

    internal readonly struct UpgradeContentAvailability
    {
        public UpgradeContentAvailability(bool completed, bool warnings, bool available)
        {
            Completed = completed;
            Warnings = warnings;
            Available = available;
        }

        public bool Completed { get; }
        public bool Warnings { get; }
        public bool Available { get; }
    }
}
