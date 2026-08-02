using System;
using Malco.Configuration.Models;
using Malco.Settings.Contracts;
using Malco.Models;

namespace Malco.Settings.Controller
{
    internal readonly struct SettingsEditResult
    {
        public SettingsEditResult(bool changed, long revision)
        {
            Changed = changed;
            Revision = revision;
        }

        public bool Changed { get; }
        public long Revision { get; }
    }

    internal readonly struct SettingsControllerCapture
    {
        public SettingsControllerCapture(long revision, HudLayoutSnapshot snapshot)
        {
            Revision = revision;
            Snapshot = snapshot;
        }

        public long Revision { get; }
        public HudLayoutSnapshot Snapshot { get; }
    }

    internal sealed partial class SettingsController
    {
        private const double DefaultCanvasWidth = 1280d;
        private const double DefaultCanvasHeight = 720d;
        private readonly object _sync = new object();
        private readonly HudLayoutConfig _layout;
        private long _editRevision;
        private SettingsPage _activeEditorPage = SettingsPage.Features;
        private string _selectedWidgetKey = HudWidgetRegistry.Units;
        private Race _selectedTechTreeRace = Race.Terran;

        public SettingsController(HudLayoutConfig initialLayout)
        {
            _layout = initialLayout ?? throw new ArgumentNullException(nameof(initialLayout));
        }

        public HudLayoutConfig Layout => _layout;

        public long EditRevision
        {
            get
            {
                lock (_sync)
                {
                    return _editRevision;
                }
            }
        }

        public SettingsPage ActiveEditorPage
        {
            get { lock (_sync) { return _activeEditorPage; } }
            set { lock (_sync) { _activeEditorPage = value == SettingsPage.Layout ? SettingsPage.Layout : SettingsPage.Features; } }
        }

        public string SelectedWidgetKey
        {
            get { lock (_sync) { return _selectedWidgetKey; } }
            set { lock (_sync) { _selectedWidgetKey = value ?? string.Empty; } }
        }

        public Race SelectedTechTreeRace
        {
            get { lock (_sync) { return _selectedTechTreeRace; } }
            set { lock (_sync) { _selectedTechTreeRace = value; } }
        }

        public SettingsEditResult ApplyEdit(SettingsEdit edit)
        {
            if (edit == null || !edit.HasTarget)
            {
                return new SettingsEditResult(false, EditRevision);
            }

            lock (_sync)
            {
                var changed = ApplyEditLocked(edit);
                if (changed)
                {
                    _editRevision = checked(_editRevision + 1L);
                }

                return new SettingsEditResult(changed, _editRevision);
            }
        }

        public SettingsControllerCapture Capture()
        {
            lock (_sync)
            {
                return new SettingsControllerCapture(
                    _editRevision,
                    HudLayoutSnapshot.FromLayout(_layout));
            }
        }

        private bool ApplyEditLocked(SettingsEdit edit)
        {
            switch (edit.Kind)
            {
                case SettingsEditKind.SetWidgetEnabled:
                    return SetWidgetEnabled(edit.Key, edit.BooleanValue);
                case SettingsEditKind.SetWidgetBounds:
                    return SetWidgetBounds(edit.Key, edit.Bounds);
                case SettingsEditKind.SetItemShown:
                    return SetItemShown(edit.Key, edit.BooleanValue);
                case SettingsEditKind.SetItemsShown:
                    return SetItemsShown(edit.Keys, edit.BooleanValue);
                case SettingsEditKind.SetAvailableAlert:
                    return SetAvailableAlert(edit.Key, edit.BooleanValue);
                case SettingsEditKind.SetAvailableAlerts:
                    return SetAvailableAlerts(edit.Keys, edit.BooleanValue);
                case SettingsEditKind.SetCompletionAlert:
                    return SetCompletionAlert(edit.Key, edit.BooleanValue);
                case SettingsEditKind.SetCompletionAlerts:
                    return SetCompletionAlerts(edit.Keys, edit.BooleanValue);
                case SettingsEditKind.SetLanguage:
                    return SetLanguage(edit.StringValue);
                case SettingsEditKind.SetCompletionDisplayMode:
                    return SetCompletionDisplayMode(edit.StringValue);
                case SettingsEditKind.SetCompletionCountdownSeconds:
                    return SetCompletionCountdownSeconds(edit.IntegerValue);
                case SettingsEditKind.SetIconSize:
                    return SetIconSize(edit.Key, edit.StringValue);
                case SettingsEditKind.SetWorkerCountStyle:
                    return SetWorkerCountStyle(edit.StringValue);
                case SettingsEditKind.SetAbilityDisplayMode:
                    return SetAbilityDisplayMode(edit.Key, edit.StringValue);
                case SettingsEditKind.SetTransportCargoVisible:
                    return SetTransportCargoVisible(edit.BooleanValue);
                case SettingsEditKind.ResetWidgetBounds:
                    return ResetWidgetBounds(edit.Key);
                case SettingsEditKind.ResetAllWidgetBounds:
                    return ResetAllWidgetBounds();
                default:
                    return false;
            }
        }

    }
}
