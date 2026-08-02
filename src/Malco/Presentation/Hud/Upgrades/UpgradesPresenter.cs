using System;
using System.Collections.Generic;
using Malco.Models;

namespace Malco.Presentation.Hud.Upgrades
{
    internal sealed class UpgradesPresenter
    {
        private readonly CompletedUpgradesPresenter _completed;
        private readonly UpgradeWarningsPresenter _warnings;
        private readonly AvailableUpgradesPresenter _available;
        private long _sessionGeneration = -1;

        public UpgradesPresenter(CompletedUpgradesPresenter completed, UpgradeWarningsPresenter warnings, AvailableUpgradesPresenter available)
        {
            _completed = completed ?? throw new ArgumentNullException(nameof(completed));
            _warnings = warnings ?? throw new ArgumentNullException(nameof(warnings));
            _available = available ?? throw new ArgumentNullException(nameof(available));
        }

        public CompletedUpgradeViewHandles CompletedView => _completed.View;
        public System.Windows.Controls.StackPanel WarningView => _warnings.View;
        public AvailableUpgradeViewHandles AvailableView => _available.View;
        public bool NeedsClockRefresh => _warnings.NeedsClockRefresh;

        public UpgradeContentAvailability ApplySlowState(UpgradePresentationInput input)
        {
            EnsureSession(input.SessionGeneration);
            IList<UpgradeState> allStates;
            var completed = _completed.Apply(input, out allStates);
            var warnings = _warnings.Apply(input, allStates);
            var available = _available.Apply(input);
            return new UpgradeContentAvailability(completed, warnings, available);
        }

        public UpgradeContentAvailability ApplyCompletedAndWarnings(UpgradePresentationInput input)
        {
            EnsureSession(input.SessionGeneration);
            IList<UpgradeState> allStates;
            var completed = _completed.Apply(input, out allStates);
            var warnings = _warnings.Apply(input, allStates);
            return new UpgradeContentAvailability(completed, warnings, false);
        }

        public bool ApplyAvailability(UpgradePresentationInput input)
        {
            EnsureSession(input.SessionGeneration);
            return _available.Apply(input);
        }

        public void AdvanceClock(DateTime now) => _warnings.AdvanceClock(now);

        public void ResetSession(long generation)
        {
            _sessionGeneration = generation;
            _completed.Reset();
            _warnings.Clear();
            _available.Clear();
        }

        public void InvalidateVisuals()
        {
            _completed.Invalidate();
            _warnings.Clear();
            _available.Invalidate();
        }

        private void EnsureSession(long generation)
        {
            if (_sessionGeneration != generation) ResetSession(generation);
        }
    }
}
