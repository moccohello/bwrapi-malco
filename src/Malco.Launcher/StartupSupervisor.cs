using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace Malco.Launcher
{
    internal sealed class StartupSupervisor
    {
        private const string MalcoMutexName = @"Local\Malco.Desktop.SingleInstance.v1";
        private readonly InstallStateStore _stateStore;
        private readonly ReleaseVerifier _verifier;
        private readonly LauncherPolicy _policy;
        private readonly string _uiLanguage;

        public StartupSupervisor(
            InstallStateStore stateStore,
            ReleaseVerifier verifier,
            LauncherPolicy policy,
            string uiLanguage)
        {
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
            _verifier = verifier ?? throw new ArgumentNullException(nameof(verifier));
            _policy = policy ?? throw new ArgumentNullException(nameof(policy));
            _uiLanguage = uiLanguage ?? throw new ArgumentNullException(nameof(uiLanguage));
        }

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

        public void LaunchStable(
            ReleaseReference reference,
            bool requiredUpdateRecheck = false)
        {
            var activationId = _stateStore.NewActivationId();
            try
            {
                if (!LaunchAndAwaitHandshake(
                        reference,
                        activationId,
                        requiredUpdateRecheck: requiredUpdateRecheck))
                {
                    throw new InvalidDataException(
                        "Malco did not complete its startup handshake.");
                }
                UpdateInstalledProductVersion(reference);
            }
            finally
            {
                _stateStore.DeleteMarker(activationId);
            }
        }

        public void UpdateInstalledProductVersion(ReleaseReference reference)
        {
            var versionDirectory = _stateStore.VersionDirectory(reference);
            var envelope = _verifier.VerifyInstalledRelease(reference, versionDirectory);
            InstalledProductRegistration.TrySetVersion(envelope.Manifest.Version);
        }

        public bool LaunchAndAwaitHandshake(
            ReleaseReference reference,
            string activationId,
            Action<int, long> processStarted = null,
            bool requiredUpdateRecheck = false)
        {
            _stateStore.DeleteMarker(activationId);
            using (var process = Start(reference, activationId, requiredUpdateRecheck))
            {
                try
                {
                    processStarted?.Invoke(
                        process.Id,
                        process.StartTime.ToUniversalTime().Ticks);
                }
                catch
                {
                    if (!Terminate(process)) throw new CandidateStillRunningException();
                    throw;
                }
                try
                {
                    return AwaitHandshake(process, activationId);
                }
                catch (CandidateStillRunningException)
                {
                    throw;
                }
                catch
                {
                    if (!Terminate(process)) throw new CandidateStillRunningException();
                    throw;
                }
            }
        }

        public bool ResumePendingHandshake(
            ReleaseReference reference,
            string activationId,
            int processId,
            long processStartTimeUtcTicks)
        {
            Process process;
            try
            {
                process = Process.GetProcessById(processId);
            }
            catch (ArgumentException)
            {
                return false;
            }
            using (process)
            {
                try
                {
                    if (process.HasExited ||
                        process.StartTime.ToUniversalTime().Ticks != processStartTimeUtcTicks)
                    {
                        return false;
                    }
                }
                catch (InvalidOperationException)
                {
                    return false;
                }
                catch (Exception exception) when (
                    exception is System.ComponentModel.Win32Exception ||
                    exception is NotSupportedException ||
                    exception is UnauthorizedAccessException)
                {
                    // The persisted PID could still identify a live candidate,
                    // so selector rollback is forbidden when its identity
                    // cannot be inspected conclusively.
                    throw new CandidateStillRunningException();
                }

                try
                {
                    var versionDirectory = _stateStore.VersionDirectory(reference);
                    _verifier.VerifyInstalledRelease(reference, versionDirectory);
                    return AwaitHandshake(process, activationId);
                }
                catch (CandidateStillRunningException)
                {
                    throw;
                }
                catch
                {
                    // Once the exact PID/start-time identity is known, every
                    // failure must prove that process exited before the caller
                    // is allowed to change Current or launch LKG.
                    if (!Terminate(process)) throw new CandidateStillRunningException();
                    throw;
                }
            }
        }

        public void TerminateUnrecordedPendingCandidate(ReleaseReference reference)
        {
            var versionDirectory = _stateStore.VersionDirectory(reference);
            var expectedExecutable = Path.GetFullPath(
                Path.Combine(versionDirectory, "payload", "Malco.exe"));
            int currentSession;
            Process[] processes;
            try
            {
                currentSession = Process.GetCurrentProcess().SessionId;
                processes = Process.GetProcessesByName("Malco");
            }
            catch (Exception exception) when (
                exception is System.ComponentModel.Win32Exception ||
                exception is NotSupportedException ||
                exception is InvalidOperationException)
            {
                throw new CandidateStillRunningException();
            }

            foreach (var process in processes)
            {
                using (process)
                {
                    try
                    {
                        if (process.HasExited || process.SessionId != currentSession) continue;
                        var module = process.MainModule;
                        var executable = module == null ? null : module.FileName;
                        if (string.IsNullOrEmpty(executable))
                        {
                            throw new CandidateStillRunningException();
                        }
                        if (!string.Equals(
                                Path.GetFullPath(executable),
                                expectedExecutable,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }
                        if (!Terminate(process)) throw new CandidateStillRunningException();
                    }
                    catch (InvalidOperationException)
                    {
                        // The enumerated process exited before inspection.
                    }
                    catch (CandidateStillRunningException)
                    {
                        throw;
                    }
                    catch (Exception exception) when (
                        exception is System.ComponentModel.Win32Exception ||
                        exception is NotSupportedException ||
                        exception is UnauthorizedAccessException ||
                        exception is IOException)
                    {
                        // A same-session Malco-named process could be the
                        // unrecorded candidate. Preserve pending when its exact
                        // image identity cannot be established.
                        throw new CandidateStillRunningException();
                    }
                }
            }
        }

        private bool AwaitHandshake(Process process, string activationId)
        {
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.ElapsedMilliseconds < _policy.StartupTimeoutMilliseconds)
            {
                if (process.HasExited) return false;
                StartupMarker marker = null;
                try
                {
                    marker = _stateStore.TryReadMarker(activationId);
                }
                catch (IOException)
                {
                    // The marker is created by atomic rename. A transient read
                    // failure remains within the bounded handshake interval.
                }
                catch (Exception exception) when (
                    exception is InvalidDataException ||
                    exception is UnauthorizedAccessException)
                {
                    if (!Terminate(process)) throw new CandidateStillRunningException();
                    throw;
                }
                if (marker != null)
                {
                    if (marker.ProcessId != process.Id)
                    {
                        if (!Terminate(process)) throw new CandidateStillRunningException();
                        throw new InvalidDataException("The startup marker was written by an unexpected process.");
                    }
                    return !process.HasExited;
                }
                Thread.Sleep(_policy.StartupPollMilliseconds);
            }
            if (!Terminate(process)) throw new CandidateStillRunningException();
            return false;
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

        private bool Terminate(Process process)
        {
            try
            {
                if (process.HasExited) return true;
                process.Kill(true);
                if (!process.WaitForExit(_policy.TerminationTimeoutMilliseconds))
                {
                    return false;
                }
                return process.HasExited;
            }
            catch (InvalidOperationException)
            {
                return true;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                return false;
            }
            catch (NotSupportedException)
            {
                return false;
            }
        }
    }

    internal sealed class CandidateStillRunningException : Exception
    {
        public CandidateStillRunningException()
            : base("The candidate process could not be terminated; selector rollback was refused.")
        {
        }
    }
}
