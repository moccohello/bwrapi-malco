using System;
using System.Diagnostics;
using System.Threading;
using BwrApi.Client;
using Malco.Application.Demand;
using Malco.Diagnostics;

namespace Malco.Data
{
    internal sealed partial class BwrApiEmbeddedRuntimeProvider
    {
        private void PollSemanticLoop(BwrApiClient client, CancellationToken token)
        {
            long nextSemantic = 0;
            TimeSpan semanticInterval = TimeSpan.FromMilliseconds(
                Math.Max(1, _config.SemanticSnapshotIntervalMs));

            while (!token.IsCancellationRequested)
            {
                OverlayChannelDemand demand;
                lock (_demandGate)
                {
                    demand = _demand;
                }

                long now = Stopwatch.GetTimestamp();
                nextSemantic = EnableDeadline(demand.NeedsSemantic, nextSemantic, now);
                if (IsDue(nextSemantic, now))
                {
                    PollSemanticOnce(client, token);
                    nextSemantic = NextDeadline(now, semanticInterval);
                }

                token.ThrowIfCancellationRequested();
                int waitMilliseconds = ComputePollWaitMilliseconds(
                    Stopwatch.GetTimestamp(),
                    nextSemantic);
                _semanticWake.WaitOne(waitMilliseconds);
            }
        }

        private void PollSemanticOnce(BwrApiClient client, CancellationToken token)
        {
            try
            {
                PerformanceProbe performance = _metrics.BeginSemanticPoll();
                using BwrApiFrameLease lease = client.WaitFrame(NativePollTimeout, token);
                BwrApiFrameHeader header = lease.ReadHeader();
                BwrApiSemanticSnapshotV1 source = lease.ReadSemanticSnapshotV1();
                _metrics.RecordSemanticConversion();
                _publication.PublishSemanticObservation(header, source);
                _metrics.CompleteSemanticPoll(performance);
            }
            catch (BwrApiHostUnavailableException)
            {
                if (_publication.GetProviderChannelState().Semantic.Sequence == null)
                {
                    _publication.PublishSemanticFailure(
                        "Embedded observer is waiting for a semantic frame",
                        ProviderStatus.NotReady);
                }
            }
            catch (BwrApiNativeCancelledException) when (token.IsCancellationRequested)
            {
                throw new OperationCanceledException(token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (BwrApiNativeArtifactException)
            {
                throw;
            }
            catch (Exception exception)
            {
                _publication.PublishSemanticFailure(
                    "Semantic observer error: " + exception.Message);
            }
        }
    }
}
