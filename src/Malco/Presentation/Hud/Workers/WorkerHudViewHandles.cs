using System;
using System.Windows.Controls;

namespace Malco.Presentation.Hud.Workers
{
    internal sealed class WorkerHudViewHandles
    {
        public WorkerHudViewHandles(
            StackPanel body,
            Image idleWorkerIcon,
            Image totalWorkerIcon,
            TextBlock idleWorkersValue,
            TextBlock totalWorkersValue,
            TextBlock idleWorkerAlertMark)
        {
            Body = body ?? throw new ArgumentNullException(nameof(body));
            IdleWorkerIcon = idleWorkerIcon ?? throw new ArgumentNullException(nameof(idleWorkerIcon));
            TotalWorkerIcon = totalWorkerIcon ?? throw new ArgumentNullException(nameof(totalWorkerIcon));
            IdleWorkersValue = idleWorkersValue ?? throw new ArgumentNullException(nameof(idleWorkersValue));
            TotalWorkersValue = totalWorkersValue ?? throw new ArgumentNullException(nameof(totalWorkersValue));
            IdleWorkerAlertMark = idleWorkerAlertMark ?? throw new ArgumentNullException(nameof(idleWorkerAlertMark));
        }

        public StackPanel Body { get; }

        public Image IdleWorkerIcon { get; }

        public Image TotalWorkerIcon { get; }

        public TextBlock IdleWorkersValue { get; }

        public TextBlock TotalWorkersValue { get; }

        public TextBlock IdleWorkerAlertMark { get; }
    }
}
