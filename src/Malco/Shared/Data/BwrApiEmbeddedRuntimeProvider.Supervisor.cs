using System;
using System.Diagnostics;
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
                semanticClient = OpenObserverWithRetry(
                    () => _sessionBinding.OpenBoundClient(identity, "malco", token),
                    exception => _publication.PublishSemanticFailure(
                        "Embedded observer is waiting to connect: " + exception.Message,
                        ProviderStatus.NotReady),
                    token);

                // SessionGeneration belongs to one BwrApiClient observation history.
                // Keep semantic and projection polling on separate workers, but share
                // the observer so save/replay transitions have one session identity.
                BwrApiClient capturedProjectionClient = semanticClient;
                projectionWorker = Task.Run(
                    () => RunProjectionWorker(capturedProjectionClient, token));
                PollSemanticLoop(semanticClient, identity, token);
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

        internal static T OpenObserverWithRetry<T>(
            Func<T> openObserver,
            Action<BwrApiHostUnavailableException> onUnavailable,
            CancellationToken token,
            TimeSpan? retryTimeout = null)
        {
            var retryWindow = retryTimeout ?? TimeSpan.FromSeconds(10);
            if (retryWindow < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(retryTimeout));
            var elapsed = Stopwatch.StartNew();
            while (true)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    return openObserver();
                }
                catch (BwrApiHostUnavailableException exception)
                {
                    token.ThrowIfCancellationRequested();
                    // The SDK also uses this exception for persistent access
                    // failures. Bound retries instead of silently waiting forever.
                    var remaining = retryWindow - elapsed.Elapsed;
                    if (remaining <= TimeSpan.Zero)
                    {
                        throw new TimeoutException(
                            "Embedded observer connection retries timed out; " +
                            "check StarCraft process access. Last error: " + exception.Message,
                            exception);
                    }
                    onUnavailable(exception);
                    token.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(
                        Math.Max(1d, Math.Min(250d, remaining.TotalMilliseconds))));
                }
            }
        }
    }
}
