using System;

namespace Malco.Launcher
{
    internal static partial class Program
    {
        private static int? RecoverPending(
            InstallStateStore stateStore,
            StagedUpdateStore stagedStore,
            ReleaseVerifier verifier,
            StartupSupervisor supervisor,
            InstallState state)
        {
            if (state.Pending == null) return null;

            if (state.Pending.ProcessId.HasValue)
            {
                try
                {
                    var recovered = supervisor.ResumePendingHandshake(
                        state.Pending.Candidate,
                        state.Pending.ActivationId,
                        state.Pending.ProcessId.Value,
                        state.Pending.ProcessStartTimeUtcTicks.Value);
                    if (recovered)
                    {
                        CommitPendingActivation(
                            stateStore,
                            stagedStore,
                            verifier,
                            supervisor,
                            state);
                        return (int)ExitCode.Success;
                    }
                    return RollBackAndLaunch(
                        stateStore,
                        stagedStore,
                        verifier,
                        supervisor,
                        state,
                        state.Pending.UpdateRequirement);
                }
                catch (CandidateStillRunningException)
                {
                    return (int)ExitCode.StartupFailed;
                }
                catch (Exception ex) when (IsLaunchFailure(ex))
                {
                    return RollBackAndLaunch(
                        stateStore,
                        stagedStore,
                        verifier,
                        supervisor,
                        state,
                        state.Pending.UpdateRequirement);
                }
            }
            if (state.Pending.StartupAttempts > 0)
            {
                try
                {
                    supervisor.TerminateUnrecordedPendingCandidate(state.Pending.Candidate);
                }
                catch (Exception ex) when (ex is CandidateStillRunningException || IsLaunchFailure(ex))
                {
                    // An attempt was durably counted before its PID could be
                    // saved. Preserve pending unless every exact candidate
                    // image in this session is conclusively absent or stopped.
                    return (int)ExitCode.StartupFailed;
                }
            }
            if (supervisor.IsMalcoRunning(state.Pending.Candidate))
            {
                return (int)ExitCode.StartupFailed;
            }
            if (state.Pending.StartupAttempts < 2) return null;
            return RollBackAndLaunch(
                stateStore,
                stagedStore,
                verifier,
                supervisor,
                state,
                state.Pending.UpdateRequirement);
        }

        private static void CommitPendingActivation(
            InstallStateStore stateStore,
            StagedUpdateStore stagedStore,
            ReleaseVerifier verifier,
            StartupSupervisor supervisor,
            InstallState state)
        {
            if (state.Pending == null)
            {
                throw new InvalidOperationException("A pending activation is required.");
            }

            var activationId = state.Pending.ActivationId;
            var committed = new InstallState(
                state.Generation,
                state.HighestAcceptedSequence,
                state.Current.Clone(),
                null,
                null,
                null);
            stateStore.Save(committed);
            stateStore.DeleteMarker(activationId);
            stagedStore.Delete();
            stateStore.CleanupUnreferencedVersions(
                committed,
                verifier,
                supervisor.IsMalcoRunning);
        }
    }
}
