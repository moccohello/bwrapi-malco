using System;
using Malco.Application.Demand;

namespace Malco.Application.Overlay
{
    // Owns application-level channel demand. Presentation decides what is
    // visible, while this controller owns the demand state and epoch.
    internal sealed class OverlayApplicationController
    {
        private readonly object _sync = new object();
        private readonly IOverlayDemandController _demandController;
        private OverlayChannelDemand _currentDemand = OverlayChannelDemand.All;
        private long _demandEpoch;
        private bool _initialized;

        public OverlayApplicationController(IOverlayDemandController demandController)
        {
            _demandController = demandController ?? throw new ArgumentNullException(nameof(demandController));
        }

        public bool ProjectionDemanded
        {
            get { lock (_sync) { return _currentDemand.NeedsProjection; } }
        }

        public bool CommandsDemanded
        {
            get { lock (_sync) { return _currentDemand.NeedsCommands; } }
        }

        public long DemandEpoch
        {
            get { lock (_sync) { return _demandEpoch; } }
        }

        public OverlayDemandReceipt SetChannelDemand(bool needsProjection, bool needsCommands)
        {
            lock (_sync)
            {
                if (_initialized &&
                    _currentDemand.NeedsProjection == needsProjection &&
                    _currentDemand.NeedsCommands == needsCommands)
                {
                    return new OverlayDemandReceipt(_demandEpoch, _currentDemand);
                }

                var receipt = _demandController.SetDemand(
                    new OverlayChannelDemand(true, needsProjection, needsCommands));
                _currentDemand = receipt.Demand;
                _demandEpoch = receipt.Epoch;
                _initialized = true;
                return receipt;
            }
        }
    }
}
