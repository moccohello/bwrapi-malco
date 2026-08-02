using System;

namespace Malco.Application.Demand
{
    internal sealed class OverlayChannelDemand
    {
        public OverlayChannelDemand(bool needsSemantic, bool needsProjection, bool needsCommands)
        {
            NeedsSemantic = needsSemantic;
            NeedsProjection = needsProjection;
            NeedsCommands = needsCommands;
        }

        public bool NeedsSemantic { get; }
        public bool NeedsProjection { get; }
        public bool NeedsCommands { get; }

        public static OverlayChannelDemand All { get; } = new OverlayChannelDemand(true, true, true);
    }

    internal readonly struct OverlayDemandReceipt
    {
        public OverlayDemandReceipt(long epoch, OverlayChannelDemand demand)
        {
            Epoch = epoch;
            Demand = demand ?? throw new ArgumentNullException(nameof(demand));
        }

        public long Epoch { get; }
        public OverlayChannelDemand Demand { get; }
    }

    internal interface IOverlayDemandController
    {
        OverlayDemandReceipt SetDemand(OverlayChannelDemand demand);
    }
}
