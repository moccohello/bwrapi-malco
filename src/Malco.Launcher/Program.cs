using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;

namespace Malco.Launcher
{
    internal static partial class Program
    {
        private static LauncherLanguage _language;

        private enum ExitCode
        {
            Success = 0,
            LauncherAlreadyRunning = 10,
            PolicyInvalid = 20,
            StateInvalid = 21,
            NoValidRelease = 22,
            StartupFailed = 23,
            UnexpectedFailure = 24,
            RequiredUpdateFailed = 25,
            RequiredUpdateAvailable = 30,
            RequiredUpdateCheckUnavailable = 31
        }

        [STAThread]
        private static int Main(string[] args)
        {
            var executableName = Path.GetFileName(Environment.ProcessPath ?? string.Empty);
            var primaryLauncher = string.Equals(
                executableName,
                "Malco.Launcher.exe",
                StringComparison.OrdinalIgnoreCase);
            var requiredUpdateCheck = args.Length == 2 &&
                string.Equals(args[0], "--check-required-update", StringComparison.Ordinal) &&
                ContractCodec.IsLowerSha256(args[1]);
            if (!primaryLauncher || (args.Length != 0 && !requiredUpdateCheck))
            {
                return (int)ExitCode.UnexpectedFailure;
            }

            _language = LauncherLanguage.Resolve(Path.GetFullPath(AppContext.BaseDirectory));

            bool createdNew;
            using (var mutex = new Mutex(true, UpdateRuntimeNames.LauncherMutexName, out createdNew))
            {
                if (!createdNew) return (int)ExitCode.LauncherAlreadyRunning;
                ExitCode result;
                try
                {
                    result = requiredUpdateCheck
                        ? (ExitCode)RunRequiredUpdateCheck(args[1])
                        : (ExitCode)Run();
                }
                catch (CryptographicException)
                {
                    result = ExitCode.PolicyInvalid;
                }
                catch (InvalidDataException)
                {
                    result = ExitCode.StateInvalid;
                }
                catch (Exception)
                {
                    result = ExitCode.UnexpectedFailure;
                }
                if (!requiredUpdateCheck) ShowFailure(result);
                return (int)result;
            }
        }

        private static int Run()
        {
            var installRoot = Path.GetFullPath(AppContext.BaseDirectory);
            var stateStore = new InstallStateStore(installRoot);
            stateStore.EnsureRoots();

            LauncherPolicy policy;
            try
            {
                policy = ContractCodec.ParsePolicy(ReleaseVerifier.ReadBoundedFile(
                    stateStore.PolicyPath,
                    LauncherPolicy.BootstrapMaximumPolicyBytes));
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException ||
                                       ex is InvalidDataException || ex is FormatException)
            {
                return (int)ExitCode.PolicyInvalid;
            }

            stateStore.ConfigurePolicy(policy);
            var verifier = new ReleaseVerifier(policy);
            var supervisor = new StartupSupervisor(stateStore, verifier, policy, _language.Code);
            var stagedStore = new StagedUpdateStore(stateStore, policy);
            InstallState state;
            try
            {
                state = stateStore.Load();
                stateStore.CleanupStaging(verifier);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException ||
                                       ex is InvalidDataException)
            {
                return (int)ExitCode.StateInvalid;
            }

            var recoveryResult = RecoverPending(
                stateStore,
                stagedStore,
                verifier,
                supervisor,
                state);
            if (recoveryResult.HasValue) return recoveryResult.Value;
            state = stateStore.Load();
            var stagedUpdate = LoadVerifiedStagedUpdate(
                stateStore,
                stagedStore,
                verifier);
            if (state.Current != null && supervisor.IsMalcoRunning(state.Current))
            {
                stateStore.CleanupUnreferencedVersions(
                    state,
                    verifier,
                    supervisor.IsMalcoRunning,
                    stagedUpdate.Candidate);
                return (int)ExitCode.Success;
            }

            var currentValid = state.Current != null;
            if (currentValid)
            {
                try
                {
                    // Only a completely reverified stable current may become
                    // the last-known-good side of a new atomic selection.
                    verifier.VerifyInstalledRelease(
                        state.Current,
                        stateStore.VersionDirectory(state.Current));
                }
                catch (Exception ex) when (IsLaunchFailure(ex))
                {
                    currentValid = false;
                }
            }

            var startupUpdateDisposition = StartupUpdateDisposition.Continue;
            var candidateRequirement = state.Pending?.UpdateRequirement ?? UpdateRequirement.Optional;
            var requiredUpdateRecheck = false;
            if (state.Pending == null)
            {
                var startupUpdate = StageStartupUpdate(
                    stateStore,
                    stagedStore,
                    verifier,
                    policy,
                    state,
                    stagedUpdate,
                    out requiredUpdateRecheck);
                startupUpdateDisposition = startupUpdate.Disposition;
                if (startupUpdateDisposition == StartupUpdateDisposition.RequiredUpdateFailed)
                {
                    return (int)ExitCode.RequiredUpdateFailed;
                }
                if (startupUpdate.Candidate != null)
                {
                    candidateRequirement = startupUpdate.Requirement;
                }
                state = ActivateStagedUpdate(
                    stateStore,
                    stagedStore,
                    state,
                    startupUpdate.Candidate,
                    candidateRequirement,
                    ref currentValid);
            }

            stateStore.CleanupUnreferencedVersions(
                state,
                verifier,
                supervisor.IsMalcoRunning);
            if (startupUpdateDisposition == StartupUpdateDisposition.RequiredActivationPending &&
                state.Pending == null)
            {
                return (int)ExitCode.RequiredUpdateFailed;
            }
            if (state.Current == null) return (int)ExitCode.NoValidRelease;

            if (state.Pending == null)
            {
                if (!currentValid) return (int)ExitCode.NoValidRelease;
                try
                {
                    supervisor.LaunchStable(state.Current, requiredUpdateRecheck);
                    return (int)ExitCode.Success;
                }
                catch (Exception ex) when (IsLaunchFailure(ex))
                {
                    return (int)ExitCode.StartupFailed;
                }
            }

            bool started;
            if (state.Pending.StartupAttempts >= 2)
            {
                return RollBackAndLaunch(
                    stateStore,
                    stagedStore,
                    verifier,
                    supervisor,
                    state,
                    candidateRequirement,
                    requiredUpdateRecheck);
            }
            state = state.WithPending(
                state.Pending
                    .WithStartupAttempt(state.Pending.StartupAttempts + 1)
                    .WithProcess(null, null));
            stateStore.Save(state);
            try
            {
                started = supervisor.LaunchAndAwaitHandshake(
                    state.Pending.Candidate,
                    state.Pending.ActivationId,
                    (processId, startTimeUtcTicks) =>
                    {
                        state = state.WithPending(
                            state.Pending.WithProcess(processId, startTimeUtcTicks));
                        stateStore.Save(state);
                    },
                    requiredUpdateRecheck);
            }
            catch (CandidateStillRunningException)
            {
                // Never change the selector or launch LKG while the exact
                // candidate could still be alive after a failed termination.
                return (int)ExitCode.StartupFailed;
            }
            catch (Exception ex) when (IsLaunchFailure(ex))
            {
                started = false;
            }
            if (started)
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
                candidateRequirement,
                requiredUpdateRecheck);
        }
    }
}
