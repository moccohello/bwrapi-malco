using System;
using System.Diagnostics;
using System.IO;

namespace Malco.Launcher
{
    internal sealed partial class StartupSupervisor
    {
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

        private bool Terminate(Process process)
        {
            try
            {
                if (process.HasExited) return true;
                process.Kill(true);
                return process.WaitForExit(_policy.TerminationTimeoutMilliseconds);
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
