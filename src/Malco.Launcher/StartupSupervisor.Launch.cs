using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace Malco.Launcher
{
    internal sealed partial class StartupSupervisor
    {
        public bool IsMalcoRunning(ReleaseReference reference)
        {
            if (reference == null || !ContractCodec.IsLowerSha256(reference.ManifestSha256))
            {
                return false;
            }
            Mutex mutex;
            try
            {
                if (!Mutex.TryOpenExisting(
                        MalcoMutexName + ".Release." + reference.ManifestSha256,
                        out mutex)) return false;
                mutex.Dispose();
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                return true;
            }
        }

        private Process Start(
            ReleaseReference reference,
            string activationId,
            bool requiredUpdateRecheck)
        {
            var versionDirectory = _stateStore.VersionDirectory(reference);
            _verifier.VerifyInstalledRelease(reference, versionDirectory);
            var payloadDirectory = Path.Combine(versionDirectory, "payload");
            var executable = Path.Combine(payloadDirectory, "Malco.exe");
            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                WorkingDirectory = payloadDirectory,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.Environment[LauncherLanguage.ProcessEnvironmentName] = _uiLanguage;
            if (activationId != null && !ContractCodec.IsActivationId(activationId))
            {
                throw new InvalidDataException("The startup activation ID is invalid.");
            }
            var launchToken = _stateStore.NewActivationId();
            startInfo.ArgumentList.Add("--launch-token");
            startInfo.ArgumentList.Add(launchToken);
            if (activationId != null)
            {
                startInfo.ArgumentList.Add("--startup-token");
                startInfo.ArgumentList.Add(activationId);
            }
            Process process = null;
            try
            {
                _stateStore.WriteLaunchAuthorization(
                    launchToken,
                    reference,
                    requiredUpdateRecheck);
                process = Process.Start(startInfo);
                if (process == null)
                {
                    throw new InvalidOperationException("Malco could not be started.");
                }
                AwaitLaunchAuthorizationConsumption(process, launchToken);
                AwaitSelectedReleaseMutex(process, reference);
                return process;
            }
            catch
            {
                var terminated = process == null || Terminate(process);
                process?.Dispose();
                try
                {
                    _stateStore.DeleteLaunchAuthorization(launchToken);
                }
                catch (Exception exception) when (
                    exception is IOException || exception is UnauthorizedAccessException)
                {
                    if (!terminated) throw new CandidateStillRunningException();
                    throw new InvalidDataException(
                        "A failed launch authorization could not be removed.",
                        exception);
                }
                if (!terminated) throw new CandidateStillRunningException();
                throw;
            }
        }

        private void AwaitLaunchAuthorizationConsumption(Process process, string launchToken)
        {
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.ElapsedMilliseconds < _policy.AuthorizationTimeoutMilliseconds)
            {
                if (process.HasExited)
                {
                    throw new InvalidDataException("Malco rejected its launch authorization.");
                }
                if (!_stateStore.LaunchAuthorizationExists(launchToken)) return;
                Thread.Sleep(_policy.AuthorizationPollMilliseconds);
            }
            throw new InvalidDataException("Malco did not consume its launch authorization.");
        }

        private void AwaitSelectedReleaseMutex(Process process, ReleaseReference reference)
        {
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.ElapsedMilliseconds < _policy.AuthorizationTimeoutMilliseconds)
            {
                if (process.HasExited)
                {
                    throw new InvalidDataException("Malco exited before owning its selected-release mutex.");
                }
                if (IsMalcoRunning(reference))
                {
                    Thread.Sleep(_policy.SelectedReleaseStabilityMilliseconds);
                    if (!process.HasExited && IsMalcoRunning(reference)) return;
                    throw new InvalidDataException("Malco did not retain its selected-release mutex.");
                }
                Thread.Sleep(_policy.AuthorizationPollMilliseconds);
            }
            throw new InvalidDataException("Malco did not own its selected-release mutex in time.");
        }
    }
}
