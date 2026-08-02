using System;

namespace Malco.Launcher
{
    internal static partial class Program
    {
        private static int RollBackAndLaunch(
            InstallStateStore stateStore,
            StagedUpdateStore stagedStore,
            ReleaseVerifier verifier,
            StartupSupervisor supervisor,
            InstallState state,
            UpdateRequirement requirement,
            bool requiredUpdateRecheck = false)
        {
            var pending = state.Pending;
            if (pending == null) return (int)ExitCode.StartupFailed;
            var requiredCandidate = requirement == UpdateRequirement.Required;
            try
            {
                verifier.VerifyInstalledRelease(
                    pending.Candidate,
                    stateStore.VersionDirectory(pending.Candidate));
            }
            catch (Exception ex) when (IsLaunchFailure(ex))
            {
                // The persisted requirement remains authoritative if the
                // candidate cannot be reverified during recovery.
            }
            if (!pending.RollbackAvailable)
            {
                // The signed installer baseline has no older safe target. A
                // recoverable first-start failure requiring user action must
                // remain retryable
                // instead of consuming the only installed release.
                stateStore.DeleteMarker(pending.ActivationId);
                stagedStore.Delete();
                stateStore.Save(state.WithPending(pending.WithStartupAttempt(0).WithProcess(null, null)));
                return requiredCandidate
                    ? (int)ExitCode.RequiredUpdateFailed
                    : (int)ExitCode.StartupFailed;
            }

            var target = pending.PreviousCurrent?.Clone();
            if (target != null)
            {
                try
                {
                    verifier.VerifyInstalledRelease(target, stateStore.VersionDirectory(target));
                }
                catch (Exception ex) when (IsLaunchFailure(ex))
                {
                    target = null;
                }
            }

            var rolledBack = new InstallState(
                state.Generation,
                state.HighestAcceptedSequence,
                target?.Clone(),
                null,
                null,
                new RollbackRecord(
                    pending.Candidate.Clone(),
                    target?.Clone()));
            stateStore.DeleteMarker(pending.ActivationId);
            stateStore.Save(rolledBack);
            stagedStore.Delete();
            stateStore.CleanupUnreferencedVersions(
                rolledBack,
                verifier,
                supervisor.IsMalcoRunning);
            if (target == null) return (int)ExitCode.StartupFailed;
            if (requiredCandidate) return (int)ExitCode.RequiredUpdateFailed;

            var rollbackActivation = stateStore.NewActivationId();
            try
            {
                return supervisor.LaunchAndAwaitHandshake(
                    target,
                    rollbackActivation,
                    requiredUpdateRecheck: requiredUpdateRecheck)
                    ? (int)ExitCode.Success
                    : (int)ExitCode.StartupFailed;
            }
            catch (Exception ex) when (IsLaunchFailure(ex))
            {
                return (int)ExitCode.StartupFailed;
            }
            finally
            {
                stateStore.DeleteMarker(rollbackActivation);
            }
        }
    }
}
