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
        private void RunProjectionWorker(BwrApiClient client, CancellationToken token)
        {
            try
            {
                PollProjectionLoop(client, token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
            }
            catch (BwrApiNativeArtifactException exception)
            {
                if (!IsClosing)
                {
                    _publication.PublishFatalFailure(
                        "Embedded projection artifact failed: " + exception.Message);
                    BeginStop();
                }
            }
            catch (Exception exception)
            {
                if (!IsClosing)
                {
                    _publication.PublishFatalFailure(
                        "Embedded projection scheduler failed: " + exception);
                    BeginStop();
                }
            }
        }

        private void PollProjectionLoop(BwrApiClient client, CancellationToken token)
        {
            long nextViewport = 0;
            long nextCommands = 0;
            while (!token.IsCancellationRequested)
            {
                OverlayChannelDemand demand;
                long demandEpoch;
                lock (_demandGate)
                {
                    demand = _demand;
                    demandEpoch = _demandEpoch;
                }

                long now = Stopwatch.GetTimestamp();
                nextViewport = EnableDeadline(demand.NeedsProjection, nextViewport, now);
                nextCommands = EnableDeadline(demand.NeedsCommands, nextCommands, now);
                if (IsDue(nextViewport, now))
                {
                    PollViewportOnce(client, demandEpoch);
                    nextViewport = NextDeadline(now, ViewportActiveInterval);
                }

                now = Stopwatch.GetTimestamp();
                if (IsDue(nextCommands, now))
                {
                    PollCommandsOnce(client, demandEpoch);
                    nextCommands = NextDeadline(now, CommandActiveInterval);
                }

                token.ThrowIfCancellationRequested();
                int waitMilliseconds = ComputePollWaitMilliseconds(
                    Stopwatch.GetTimestamp(),
                    nextViewport,
                    nextCommands);
                _projectionWake.WaitOne(waitMilliseconds);
            }
        }

        private void PollViewportOnce(BwrApiClient client, long demandEpoch)
        {
            try
            {
                PerformanceProbe performance = _metrics.BeginViewportPoll();
                BwrApiViewportProjectionV1 source = client.ReadViewportProjectionV1(
                    checked((ulong)demandEpoch), true);
                _publication.PublishViewportObservation(source);
                _metrics.CompleteViewportPoll(performance);
            }
            catch (BwrApiNativeArtifactException)
            {
                throw;
            }
            catch (OverflowException exception)
            {
                _publication.PublishViewportFailure(
                    "Viewport projection numeric range error: " + exception.Message,
                    ProviderStatus.Stale);
            }
            catch (BwrApiException exception)
            {
                _publication.PublishViewportFailure(
                    "Viewport projection error: " + exception.Message,
                    ProviderStatus.Stale);
            }
        }

        private void PollCommandsOnce(BwrApiClient client, long demandEpoch)
        {
            try
            {
                PerformanceProbe performance = _metrics.BeginCommandPoll();
                BwrApiSelectedCommandProjectionV1 source = client.ReadSelectedCommandProjectionV1(
                    checked((ulong)demandEpoch), true);
                _metrics.RecordCommandConversion();
                _publication.PublishCommandObservation(source);
                _metrics.CompleteCommandPoll(performance);
            }
            catch (BwrApiNativeArtifactException)
            {
                throw;
            }
            catch (OverflowException exception)
            {
                _publication.PublishCommandFailure(
                    "Command projection numeric range error: " + exception.Message);
            }
            catch (BwrApiException exception)
            {
                _publication.PublishCommandFailure(
                    "Command projection error: " + exception.Message);
            }
        }
    }
}
