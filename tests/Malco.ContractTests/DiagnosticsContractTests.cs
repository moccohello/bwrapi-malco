using System;
using Malco.Diagnostics;
using Xunit;

namespace Malco.ContractTests
{
    public sealed class DiagnosticsContractTests
    {
        [Theory]
        [InlineData(null, false)]
        [InlineData("0", false)]
        [InlineData("true", false)]
        [InlineData("1", true)]
        public void DiagnosticsRequiresExplicitOptIn(string value, bool expected)
        {
            const string name = "MALCO_DIAGNOSTICS";
            var previous = Environment.GetEnvironmentVariable(name);
            try
            {
                Environment.SetEnvironmentVariable(name, value);
                Assert.Equal(expected, DiagnosticsSwitches.PerformanceEnabled);
            }
            finally
            {
                Environment.SetEnvironmentVariable(name, previous);
            }
        }

        [Fact]
        public void PerformanceSamplesRemainBoundedAndOrdered()
        {
            var samples = new ConcurrentPerformanceRing(2);
            samples.Add(10, 100);
            samples.Add(20, 200);
            samples.Add(30, 300);

            var snapshot = samples.Capture();

            Assert.Equal(new long[] { 20, 30 }, snapshot.DurationMicroseconds);
            Assert.Equal(new long[] { 200, 300 }, snapshot.AllocatedBytes);
        }
    }
}
