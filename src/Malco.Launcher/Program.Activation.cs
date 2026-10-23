using System;
using System.IO;
using System.Security.Cryptography;

namespace Malco.Launcher
{
    internal static partial class Program
    {
        private static readonly Uri NewInstallerUri = new Uri("https://horangmalco.com/downloads/malco/latest");

        internal enum StartupUpdateDisposition
        {
            Continue,
            RequiredActivationPending,
            RequiredUpdateFailed
        }

        internal readonly struct StartupUpdateResult
        {
            public StartupUpdateResult(
                StartupUpdateDisposition disposition,
                ReleaseReference candidate,
                UpdateRequirement requirement)
            {
                Disposition = disposition;
                Candidate = candidate;
                Requirement = requirement;
            }

            public StartupUpdateDisposition Disposition { get; }
            public ReleaseReference Candidate { get; }
            public UpdateRequirement Requirement { get; }
        }

        private static InstallState ActivateStagedUpdate(
            InstallStateStore stateStore,
            StagedUpdateStore stagedStore,
            InstallState state,
            ReleaseReference candidate,
            UpdateRequirement requirement,
            ref bool currentValid)
        {
            if (candidate == null)
            {
                return state;
            }

            if (candidate.Sequence < state.HighestAcceptedSequence)
            {
                stagedStore.Delete();
                return state;
            }
            if (candidate.Sequence == state.HighestAcceptedSequence)
            {
                var acceptedHash = FindAcceptedManifestHash(state);
                if (acceptedHash != null &&
                    !string.Equals(acceptedHash, candidate.ManifestSha256, StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        "A staged update reused an accepted sequence for a different release.");
                }
                stagedStore.Delete();
                return state;
            }

            var previous = currentValid ? state.Current : null;
            var selected = new InstallState(
                state.Generation,
                candidate.Sequence,
                candidate,
                previous,
                new PendingActivation(
                    candidate,
                    previous,
                    stateStore.NewActivationId(),
                    requirement,
                    previous != null,
                    0,
                    null,
                    null),
                state.LastRollback);
            stateStore.Save(selected);
            stagedStore.Delete();
            currentValid = true;
            return selected;
        }

        private static StartupUpdateResult LoadVerifiedStagedUpdate(
            InstallStateStore stateStore,
            StagedUpdateStore stagedStore,
            ReleaseVerifier verifier)
        {
            try
            {
                var candidate = stagedStore.TryLoad();
                if (candidate == null)
                {
                    return new StartupUpdateResult(
                        StartupUpdateDisposition.Continue,
                        null,
                        UpdateRequirement.Optional);
                }
                var envelope = verifier.VerifyInstalledRelease(
                    candidate,
                    stateStore.VersionDirectory(candidate));
                var disposition = string.Equals(
                    envelope.Manifest.UpdatePolicy,
                    "required",
                    StringComparison.Ordinal)
                    ? StartupUpdateDisposition.RequiredActivationPending
                    : StartupUpdateDisposition.Continue;
                return new StartupUpdateResult(
                    disposition,
                    candidate,
                    disposition == StartupUpdateDisposition.RequiredActivationPending
                        ? UpdateRequirement.Required
                        : UpdateRequirement.Optional);
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is InvalidDataException ||
                exception is CryptographicException)
            {
                stagedStore.Delete();
                return new StartupUpdateResult(
                    StartupUpdateDisposition.Continue,
                    null,
                    UpdateRequirement.Optional);
            }
        }

        private static StartupUpdateResult StageStartupUpdate(
            InstallStateStore stateStore,
            StagedUpdateStore stagedStore,
            ReleaseVerifier verifier,
            LauncherPolicy policy,
            InstallState state,
            StartupUpdateResult verifiedStagedUpdate,
            out bool requiredUpdateRecheck)
        {
            return EvaluateStartupUpdate(
                state,
                verifiedStagedUpdate,
                rollback =>
                {
                    var failedEnvelope = verifier.VerifyInstalledRelease(
                        rollback.From, stateStore.VersionDirectory(rollback.From));
                    return string.Equals(failedEnvelope.Manifest.UpdatePolicy, "required", StringComparison.Ordinal);
                },
                () =>
                {
                    using (var installer = new ReleaseInstaller(policy, verifier, stateStore))
                    {
                        return installer.FetchLatest();
                    }
                },
                (latest, required, fallback) => InstallStartupUpdate(
                    stateStore, stagedStore, verifier, policy, latest, required, fallback),
                () => LauncherDialog.ShowInstallerRequired(_language, NewInstallerUri),
                out requiredUpdateRecheck);
        }

        internal static StartupUpdateResult EvaluateStartupUpdate(
            InstallState state,
            StartupUpdateResult verifiedStagedUpdate,
            Func<RollbackRecord, bool> verifyRollbackRequired,
            Func<VerifiedEnvelope> fetchLatest,
            Func<VerifiedEnvelope, bool, StartupUpdateResult, StartupUpdateResult> installLatest,
            Action notifyInstallerRequired,
            out bool requiredUpdateRecheck)
        {
            requiredUpdateRecheck = false;
            long currentSequence = state.Current?.Sequence ?? 0;
            bool requiredBarrier = false;
            if (state.LastRollback?.From != null && state.LastRollback.From.Sequence > currentSequence)
            {
                if (state.LastRollback.UpdateRequirement.HasValue)
                {
                    requiredBarrier = state.LastRollback.UpdateRequirement == UpdateRequirement.Required;
                }
                else
                {
                    try
                    {
                        requiredBarrier = verifyRollbackRequired(state.LastRollback);
                    }
                    catch (Exception exception) when (IsLaunchFailure(exception))
                    {
                        // Unknown legacy policy cannot authorize an older offline release.
                        requiredBarrier = true;
                    }
                }
            }

            var fallback = verifiedStagedUpdate;
            if (fallback.Candidate != null && fallback.Candidate.Sequence <= state.HighestAcceptedSequence)
            {
                fallback = new StartupUpdateResult(StartupUpdateDisposition.Continue, null, UpdateRequirement.Optional);
            }
            if (requiredBarrier)
            {
                // A newer verified candidate can recover, but must inherit the
                // barrier until activation succeeds. Failure must not launch LKG.
                fallback = fallback.Candidate != null
                    ? new StartupUpdateResult(StartupUpdateDisposition.RequiredActivationPending,
                        fallback.Candidate, UpdateRequirement.Required)
                    : new StartupUpdateResult(StartupUpdateDisposition.RequiredUpdateFailed,
                        null, UpdateRequirement.Required);
            }

            VerifiedEnvelope latest;
            try
            {
                latest = fetchLatest();
            }
            catch (ReleaseExceedsPolicyException exception) when (exception.Sequence > currentSequence)
            {
                // The installed launcher cannot accept this release. Point the
                // user to the new installer, then launch as without an update.
                notifyInstallerRequired();
                return fallback;
            }
            catch (Exception exception) when (IsUpdateFailure(exception))
            {
                requiredUpdateRecheck = IsDeferredUpdateCheckFailure(exception);
                return fallback;
            }
            if (latest.Manifest.Sequence <= currentSequence) return fallback;
            bool manifestRequired = string.Equals(latest.Manifest.UpdatePolicy, "required", StringComparison.Ordinal);
            if (latest.Manifest.Sequence <= state.HighestAcceptedSequence)
            {
                // Do not retry an accepted failure. A newer verified staged
                // candidate may still repair the required rollback.
                return manifestRequired && fallback.Candidate == null
                    ? new StartupUpdateResult(StartupUpdateDisposition.RequiredUpdateFailed, null, UpdateRequirement.Required)
                    : fallback;
            }
            return installLatest(latest, manifestRequired || requiredBarrier, fallback);
        }

        private static StartupUpdateResult InstallStartupUpdate(
            InstallStateStore stateStore,
            StagedUpdateStore stagedStore,
            ReleaseVerifier verifier,
            LauncherPolicy policy,
            VerifiedEnvelope latest,
            bool required,
            StartupUpdateResult verifiedStagedUpdate)
        {
            string releaseNotes = null;
            try
            {
                using (var installer = new ReleaseInstaller(policy, verifier, stateStore))
                {
                    releaseNotes = installer.TryFetchReleaseNote(latest, _language.IsKorean);
                }
            }
            catch (Exception exception) when (IsUpdateFailure(exception))
            {
                releaseNotes = null;
            }

            ReleaseReference installedCandidate = null;
            var updateResult = UpdateDialog.ShowUpdate(
                latest,
                required,
                _language,
                progress =>
                {
                    ReleaseReference candidate;
                    using (var installer = new ReleaseInstaller(policy, verifier, stateStore))
                    {
                        candidate = installer.Install(latest, progress);
                    }
                    progress.Report(new UpdateProgress(UpdateStage.Finalizing));
                    verifier.VerifyInstalledRelease(
                        candidate,
                        stateStore.VersionDirectory(candidate));
                    stagedStore.Save(candidate);
                    installedCandidate = candidate;
                    progress.Report(new UpdateProgress(UpdateStage.Completed));
                    return candidate;
                },
                releaseNotes);
            return ResolveInstalledStartupUpdateResult(updateResult, installedCandidate, required, verifiedStagedUpdate);
        }

        internal static StartupUpdateResult ResolveInstalledStartupUpdateResult(
            UpdateDialogResult updateResult,
            ReleaseReference installedCandidate,
            bool required,
            StartupUpdateResult verifiedStagedUpdate)
        {
            if (!updateResult.Accepted)
            {
                return required
                    ? new StartupUpdateResult(
                        StartupUpdateDisposition.RequiredUpdateFailed,
                        null,
                        UpdateRequirement.Required)
                    : verifiedStagedUpdate;
            }
            if (updateResult.Error != null)
            {
                if (!IsUpdateFailure(updateResult.Error)) throw updateResult.Error;
                if (required)
                {
                    return new StartupUpdateResult(
                        StartupUpdateDisposition.RequiredUpdateFailed,
                        null,
                        UpdateRequirement.Required);
                }
                return installedCandidate == null
                    ? verifiedStagedUpdate
                    : new StartupUpdateResult(
                        StartupUpdateDisposition.Continue,
                        installedCandidate,
                        UpdateRequirement.Optional);
            }
            if (!updateResult.Installed || installedCandidate == null)
            {
                return required
                    ? new StartupUpdateResult(
                        StartupUpdateDisposition.RequiredUpdateFailed,
                        null,
                        UpdateRequirement.Required)
                    : verifiedStagedUpdate;
            }
            return new StartupUpdateResult(
                required
                    ? StartupUpdateDisposition.RequiredActivationPending
                    : StartupUpdateDisposition.Continue,
                installedCandidate,
                required ? UpdateRequirement.Required : UpdateRequirement.Optional);
        }
    }
}
