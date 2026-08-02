using System;
using System.Threading;
using Malco.Diagnostics;

namespace Malco.Data
{
    internal sealed class EmbeddedObserverMetrics
    {
        private const int PerformanceSampleCapacity = 600;

        private readonly OptInPerformanceChannel _semanticPerformance;
        private readonly OptInPerformanceChannel _viewportPerformance;
        private readonly OptInPerformanceChannel _commandPerformance;

        private long _semanticPolls;
        private long _semanticConversions;
        private long _semanticCommits;
        private long _commandPolls;
        private long _commandConversions;
        private long _commandCommits;
        private long _projectionPolls;
        private long _projectionCommits;

        public EmbeddedObserverMetrics()
        {
            bool diagnosticsEnabled = DiagnosticsSwitches.PerformanceEnabled;
            _semanticPerformance = new OptInPerformanceChannel(
                diagnosticsEnabled,
                PerformanceSampleCapacity);
            _viewportPerformance = new OptInPerformanceChannel(
                diagnosticsEnabled,
                PerformanceSampleCapacity);
            _commandPerformance = new OptInPerformanceChannel(
                diagnosticsEnabled,
                PerformanceSampleCapacity);
        }

        public PerformanceProbe BeginSemanticPoll()
        {
            PerformanceProbe performance = _semanticPerformance.Begin();
            Interlocked.Increment(ref _semanticPolls);
            return performance;
        }

        public void RecordSemanticConversion() =>
            Interlocked.Increment(ref _semanticConversions);

        public void CompleteSemanticPoll(PerformanceProbe performance) =>
            _semanticPerformance.Complete(performance);

        public PerformanceProbe BeginViewportPoll()
        {
            PerformanceProbe performance = _viewportPerformance.Begin();
            Interlocked.Increment(ref _projectionPolls);
            return performance;
        }

        public void CompleteViewportPoll(PerformanceProbe performance) =>
            _viewportPerformance.Complete(performance);

        public PerformanceProbe BeginCommandPoll()
        {
            PerformanceProbe performance = _commandPerformance.Begin();
            Interlocked.Increment(ref _commandPolls);
            return performance;
        }

        public void RecordCommandConversion() =>
            Interlocked.Increment(ref _commandConversions);

        public void CompleteCommandPoll(PerformanceProbe performance) =>
            _commandPerformance.Complete(performance);

        public void RecordProjectionChannelResetCommits()
        {
            Interlocked.Increment(ref _projectionCommits);
            Interlocked.Increment(ref _commandCommits);
        }

        public void RecordSemanticCommit() =>
            Interlocked.Increment(ref _semanticCommits);

        public void RecordCommandCommit() =>
            Interlocked.Increment(ref _commandCommits);

        public void RecordProjectionCommit() =>
            Interlocked.Increment(ref _projectionCommits);

        public void RecordFatalCommits()
        {
            Interlocked.Increment(ref _semanticCommits);
            Interlocked.Increment(ref _commandCommits);
            Interlocked.Increment(ref _projectionCommits);
        }

        public ProviderOptimizationCounters GetOptimizationCounters() =>
            new ProviderOptimizationCounters(
                Interlocked.Read(ref _semanticPolls),
                Interlocked.Read(ref _semanticConversions),
                Interlocked.Read(ref _semanticCommits),
                Interlocked.Read(ref _commandPolls),
                Interlocked.Read(ref _commandConversions),
                Interlocked.Read(ref _commandCommits),
                Interlocked.Read(ref _projectionPolls),
                Interlocked.Read(ref _projectionCommits));

        public ProviderPerformanceCounters GetPerformanceCounters() =>
            new ProviderPerformanceCounters(
                _semanticPerformance.Capture(),
                _viewportPerformance.Capture(),
                _commandPerformance.Capture());
    }
}
