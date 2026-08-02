using System;
using System.Threading;
using System.Threading.Tasks;
using BwrApi.Client;

namespace Malco.Data
{
    internal sealed partial class BwrApiEmbeddedRuntimeProvider
    {
        private void RunSupervisor(CancellationToken token)
        {
            BwrApiClient semanticClient = null;
            Task projectionWorker = null;
            try
            {
                TrackedProcessIdentity identity =
                    _sessionBinding.WaitForTrackedProcess(token);
                semanticClient = _sessionBinding.OpenBoundClient(
                    identity,
                    "malco",
                    token);

                // SessionGeneration belongs to one BwrApiClient observation history.
                // Keep semantic and projection polling on separate workers, but share
                // the observer so save/replay transitions have one session identity.
                BwrApiClient capturedProjectionClient = semanticClient;
                projectionWorker = Task.Run(
                    () => RunProjectionWorker(capturedProjectionClient, token));
                PollSemanticLoop(semanticClient, token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                if (!IsClosing)
                {
                    _publication.PublishFatalFailure(
                        "Embedded observer failed: " + exception.Message);
                    BeginStop();
                }
            }
            finally
            {
                _projectionWake.Set();
                if (projectionWorker != null)
                {
                    projectionWorker.GetAwaiter().GetResult();
                }
                _sessionBinding.DetachClient(semanticClient);
                try { semanticClient?.Dispose(); } catch { }
            }
        }
    }
}
