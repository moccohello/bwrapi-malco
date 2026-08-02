using System;
using System.Threading;
using Malco.Data;

namespace Malco.Game.Services
{
    internal sealed partial class GameCoordinator
    {
        public void Dispose()
        {
            lock (_disposeSync)
            {
                if (Volatile.Read(ref _disposeCompleted) != 0)
                {
                    return;
                }

                if (Volatile.Read(ref _closing) == 0)
                {
                    lock (_publicationSync)
                    {
                        Volatile.Write(ref _closing, 1);
                        _stateCommitSinks.Clear();
                    }
                    _providerCommitSource.UnregisterCommitSink(this);
                    try
                    {
                        _providerLifecycle.BeginStop();
                    }
                    catch (Exception ex)
                    {
                        SetShutdownBlocked("Provider stop could not begin: " + ex.Message);
                    }
                    _workerWake.Set();
                }

                if (!WaitForWorker())
                {
                    SetShutdownBlocked("Application worker did not stop within the configured timeout.");
                    return;
                }

                if (Volatile.Read(ref _rawProviderDisposed) == 0)
                {
                    ProviderStopResult stop;
                    try
                    {
                        stop = _providerLifecycle.TryStop(_shutdownTimeout);
                    }
                    catch (Exception ex)
                    {
                        SetShutdownBlocked("Provider shutdown failed: " + ex.Message);
                        return;
                    }
                    if (!stop.IsComplete)
                    {
                        SetShutdownBlocked(string.IsNullOrWhiteSpace(stop.Message)
                            ? "Provider shutdown did not complete within the configured timeout."
                            : stop.Message);
                        return;
                    }
                    Volatile.Write(ref _rawProviderDisposed, 1);
                }

                try
                {
                    _workerWake.Dispose();
                }
                catch
                {
                    SetShutdownBlocked("Application shutdown resource cleanup failed.");
                    return;
                }

                Volatile.Write(ref _shutdownBlocked, 0);
                Volatile.Write(ref _shutdownFailureMessage, string.Empty);
                Volatile.Write(ref _disposeCompleted, 1);
            }
        }

        private bool WaitForWorker()
        {
            try
            {
                return _dataCollectionTask.Wait(_shutdownTimeout);
            }
            catch
            {
                return _dataCollectionTask.IsCompleted;
            }
        }

        private void SetShutdownBlocked(string message)
        {
            Volatile.Write(ref _shutdownFailureMessage, message ?? string.Empty);
            Volatile.Write(ref _shutdownBlocked, 1);
        }

        private bool IsClosing
        {
            get { return Volatile.Read(ref _closing) != 0; }
        }
    }
}
