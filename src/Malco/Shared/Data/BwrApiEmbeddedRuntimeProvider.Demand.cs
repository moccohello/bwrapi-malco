using System;
using Malco.Application.Demand;

namespace Malco.Data
{
    internal sealed partial class BwrApiEmbeddedRuntimeProvider
    {
        public OverlayDemandReceipt SetDemand(OverlayChannelDemand demand)
        {
            if (demand == null) throw new ArgumentNullException(nameof(demand));
            lock (_demandGate)
            {
                if (IsClosing) return new OverlayDemandReceipt(_demandEpoch, _demand);
                if (_demand.NeedsSemantic == demand.NeedsSemantic &&
                    _demand.NeedsProjection == demand.NeedsProjection &&
                    _demand.NeedsCommands == demand.NeedsCommands)
                    return new OverlayDemandReceipt(_demandEpoch, _demand);

                OverlayChannelDemand previousDemand = _demand;
                _demand = demand;
                var epoch = ++_demandEpoch;
                _semanticWake.Set();
                _projectionWake.Set();
                _publication.ApplyDemandChange(previousDemand, demand, epoch);
                return new OverlayDemandReceipt(epoch, demand);
            }
        }
    }
}
