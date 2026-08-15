using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace Malco.Launcher
{
    internal sealed partial class StartupSupervisor
    {
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
                    throw;
                }
                if (marker != null)
                {
                    if (marker.ProcessId != process.Id)
                    {
                        throw new InvalidDataException("The startup marker was written by an unexpected process.");
                    }
                    return !process.HasExited;
                }
                Thread.Sleep(_policy.StartupPollMilliseconds);
            }
            if (!Terminate(process)) throw new CandidateStillRunningException();
            return false;
        }
    }
}
