using Malco.Diagnostics;

namespace Malco.Data
{
    internal readonly struct ProviderOptimizationCounters
    {
        public ProviderOptimizationCounters(
            long semanticPolls,
            long semanticConversions,
            long semanticCommits,
            long commandPolls,
            long commandConversions,
            long commandCommits,
            long projectionPolls,
            long projectionCommits)
        {
            SemanticPolls = semanticPolls;
            SemanticConversions = semanticConversions;
            SemanticCommits = semanticCommits;
            CommandPolls = commandPolls;
            CommandConversions = commandConversions;
            CommandCommits = commandCommits;
            ProjectionPolls = projectionPolls;
            ProjectionCommits = projectionCommits;
        }

        public long SemanticPolls { get; }
        public long SemanticConversions { get; }
        public long SemanticCommits { get; }
        public long CommandPolls { get; }
        public long CommandConversions { get; }
        public long CommandCommits { get; }
        public long ProjectionPolls { get; }
        public long ProjectionCommits { get; }
    }

    internal readonly struct ProviderPerformanceCounters
    {
        public ProviderPerformanceCounters(
            PerformanceChannelSnapshot semantic,
            PerformanceChannelSnapshot viewport,
            PerformanceChannelSnapshot commands)
        {
            Semantic = semantic;
            Viewport = viewport;
            Commands = commands;
        }

        public PerformanceChannelSnapshot Semantic { get; }
        public PerformanceChannelSnapshot Viewport { get; }
        public PerformanceChannelSnapshot Commands { get; }
    }

    internal interface IProviderOptimizationMetricsSource
    {
        ProviderOptimizationCounters GetOptimizationCounters();
        ProviderPerformanceCounters GetPerformanceCounters();
    }

}
