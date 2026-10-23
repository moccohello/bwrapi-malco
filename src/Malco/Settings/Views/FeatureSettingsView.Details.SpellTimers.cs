using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Malco.Data;
using Malco.Localization;
using Malco.Models;
using Malco.Settings.Contracts;

namespace Malco.Settings.Views
{
    internal sealed partial class FeatureSettingsView
    {
        private FrameworkElement BuildSpellTimersSettings()
        {
            var layout = _actions.LayoutSnapshot;
            var settings = new StackPanel { Margin = new Thickness(0d, 20d, 0d, 0d) };
            settings.Children.Add(BuildSwitchRow(
                _previewFactory.BuildFeatureIcon(FeatureSettingsPreviewKind.SpellTimers, 40d),
                UiText.Get("Spell timers"),
                UiText.Get("Show the remaining time of lasting spells (Lockdown, Stasis Field, Maelstrom, Irradiate with remaining damage, Dark Swarm, Disruption Web, Psionic Storm, Defensive Matrix, Ensnare, Plague) on visible units and spell areas."),
                layout.ShowSpellTimers,
                next => SettingsEdit.SetSpellTimersVisible(next),
                true));
            var race = BuildFilterGroup(UiText.Get("Race"), BuildRaceSelector());
            race.Margin = new Thickness(0d, 20d, 0d, 14d);
            settings.Children.Add(race);
            var first = true;
            foreach (var kind in SpellTimerCatalog.ForRace(_actions.SelectedTechTreeRace))
            {
                var spell = kind;
                settings.Children.Add(BuildSwitchRow(
                    _previewFactory.BuildCatalogIcon(TechTreeItem.Tech(spell.Replace(' ', '_')), 32d),
                    UiText.GameName(spell),
                    null,
                    !layout.HiddenSpellTimers.Contains(spell),
                    next => SettingsEdit.SetSpellTimerVisible(spell, next),
                    first));
                first = false;
            }
            return settings;
        }
    }
}
