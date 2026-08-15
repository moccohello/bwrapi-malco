using System;
using System.IO;
using System.Security.Cryptography;

namespace Malco.Launcher
{
    internal static partial class Program
    {
        private enum StartupUpdateDisposition
        {
            Continue,
            RequiredActivationPending,
            RequiredUpdateFailed
        }

        private readonly struct StartupUpdateResult
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
            requiredUpdateRecheck = false;
            var currentSequence = state.Current?.Sequence ?? 0;
            if (verifiedStagedUpdate.Candidate != null &&
                verifiedStagedUpdate.Candidate.Sequence <= currentSequence)
            {
                verifiedStagedUpdate = new StartupUpdateResult(
                    StartupUpdateDisposition.Continue,
                    verifiedStagedUpdate.Candidate,
                    verifiedStagedUpdate.Requirement);
            }

            if (state.LastRollback?.From != null &&
                state.LastRollback.From.Sequence > currentSequence)
            {
                try
                {
                    var failedEnvelope = verifier.VerifyInstalledRelease(
                        state.LastRollback.From,
                        stateStore.VersionDirectory(state.LastRollback.From));
                    if (string.Equals(
                            failedEnvelope.Manifest.UpdatePolicy,
                            "required",
                            StringComparison.Ordinal))
                    {
                        return new StartupUpdateResult(
                            StartupUpdateDisposition.RequiredUpdateFailed,
                            null,
                            UpdateRequirement.Required);
                    }
                }
                catch (Exception exception) when (IsLaunchFailure(exception))
                {
                    // 롤백 기록을 재검증할 수 없으면 최신 피드로 필수 여부를 판단한다.
                }
            }

            VerifiedEnvelope latest;
            try
            {
                using (var installer = new ReleaseInstaller(policy, verifier, stateStore))
                {
                    latest = installer.FetchLatest();
                }
            }
            catch (Exception exception) when (IsUpdateFailure(exception))
            {
                requiredUpdateRecheck = IsDeferredUpdateCheckFailure(exception);
                // 피드를 확인할 수 없으면 이미 검증한 staged update만 사용한다.
                return verifiedStagedUpdate;
            }

            if (latest.Manifest.Sequence <= currentSequence)
            {
                return verifiedStagedUpdate;
            }
            var required = string.Equals(
                latest.Manifest.UpdatePolicy,
                "required",
                StringComparison.Ordinal);
            if (latest.Manifest.Sequence <= state.HighestAcceptedSequence)
            {
                // 이미 실패해 롤백한 필수 버전은 다시 실행하지 않는다.
                return required
                    ? new StartupUpdateResult(
                        StartupUpdateDisposition.RequiredUpdateFailed,
                        null,
                        UpdateRequirement.Required)
                    : verifiedStagedUpdate;
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
                });
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
