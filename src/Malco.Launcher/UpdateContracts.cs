using System;
using System.Collections.Generic;

namespace Malco.Launcher
{
    internal sealed class LauncherPolicy
    {
        public const int BootstrapMaximumPolicyBytes = 256 * 1024;

        public Uri FeedUri { get; set; }
        public byte[] PublicKeySubjectPublicKeyInfo { get; set; }
        public int MaximumPolicyBytes { get; set; }
        public int MaximumEnvelopeBytes { get; set; }
        public int MaximumSignedManifestBytes { get; set; }
        public int MaximumStateBytes { get; set; }
        public int MaximumStartupMarkerBytes { get; set; }
        public int MaximumStagedUpdateBytes { get; set; }
        public int MaximumFiles { get; set; }
        public long MaximumArchiveBytes { get; set; }
        public long MaximumPayloadBytes { get; set; }
        public int MaximumArchiveEntries { get; set; }
        public int FeedTimeoutMilliseconds { get; set; }
        public int ArchiveTimeoutMilliseconds { get; set; }
        public int LauncherCoordinationTimeoutMilliseconds { get; set; }
        public int StartupTimeoutMilliseconds { get; set; }
        public int AuthorizationTimeoutMilliseconds { get; set; }
        public int TerminationTimeoutMilliseconds { get; set; }
        public int StartupPollMilliseconds { get; set; }
        public int AuthorizationPollMilliseconds { get; set; }
        public int SelectedReleaseStabilityMilliseconds { get; set; }
        public int RetainedStagedUpdateCount { get; set; }
    }

    internal sealed class VerifiedEnvelope
    {
        public byte[] EnvelopeBytes { get; set; }
        public string ManifestSha256 { get; set; }
        public ReleaseManifest Manifest { get; set; }
    }

    internal sealed class ReleaseManifest
    {
        public long Sequence { get; set; }
        public string Version { get; set; }
        public string UpdatePolicy { get; set; }
        public ReleaseArchive Archive { get; set; }
        public IReadOnlyList<ReleaseFile> Files { get; set; }
    }

    internal sealed class ReleaseArchive
    {
        public Uri Uri { get; set; }
        public long Length { get; set; }
        public string Sha256 { get; set; }
    }

    internal sealed class ReleaseFile
    {
        public string Path { get; set; }
        public long Length { get; set; }
        public string Sha256 { get; set; }
    }

    internal sealed class ReleaseReference
    {
        public ReleaseReference(long sequence, string manifestSha256)
        {
            Sequence = sequence;
            ManifestSha256 = manifestSha256;
        }

        public long Sequence { get; }
        public string ManifestSha256 { get; }

        public ReleaseReference Clone() => new ReleaseReference(Sequence, ManifestSha256);

        public bool SameAs(ReleaseReference other) =>
            other != null &&
            Sequence == other.Sequence &&
            string.Equals(ManifestSha256, other.ManifestSha256, StringComparison.Ordinal);
    }

    internal enum UpdateRequirement
    {
        Optional,
        Required
    }

    internal sealed class PendingActivation
    {
        public PendingActivation(
            ReleaseReference candidate,
            ReleaseReference previousCurrent,
            string activationId,
            UpdateRequirement updateRequirement,
            bool rollbackAvailable,
            int startupAttempts,
            int? processId,
            long? processStartTimeUtcTicks)
        {
            Candidate = candidate;
            PreviousCurrent = previousCurrent;
            ActivationId = activationId;
            UpdateRequirement = updateRequirement;
            RollbackAvailable = rollbackAvailable;
            StartupAttempts = startupAttempts;
            ProcessId = processId;
            ProcessStartTimeUtcTicks = processStartTimeUtcTicks;
        }

        public ReleaseReference Candidate { get; }
        public ReleaseReference PreviousCurrent { get; }
        public string ActivationId { get; }
        public UpdateRequirement UpdateRequirement { get; }
        public bool RollbackAvailable { get; }
        public int StartupAttempts { get; }
        public int? ProcessId { get; }
        public long? ProcessStartTimeUtcTicks { get; }

        public PendingActivation WithStartupAttempt(int startupAttempts) =>
            new PendingActivation(
                Candidate,
                PreviousCurrent,
                ActivationId,
                UpdateRequirement,
                RollbackAvailable,
                startupAttempts,
                ProcessId,
                ProcessStartTimeUtcTicks);

        public PendingActivation WithProcess(int? processId, long? processStartTimeUtcTicks) =>
            new PendingActivation(
                Candidate,
                PreviousCurrent,
                ActivationId,
                UpdateRequirement,
                RollbackAvailable,
                StartupAttempts,
                processId,
                processStartTimeUtcTicks);
    }

    internal sealed class RollbackRecord
    {
        public RollbackRecord(ReleaseReference from, ReleaseReference to)
        {
            From = from;
            To = to;
        }

        public ReleaseReference From { get; }
        public ReleaseReference To { get; }
    }

    internal sealed class InstallState
    {
        public const string SchemaName = "malco.install-state.v2";

        public InstallState(
            long generation,
            long highestAcceptedSequence,
            ReleaseReference current,
            ReleaseReference lastKnownGood,
            PendingActivation pending,
            RollbackRecord lastRollback)
        {
            Generation = generation;
            HighestAcceptedSequence = highestAcceptedSequence;
            Current = current;
            LastKnownGood = lastKnownGood;
            Pending = pending;
            LastRollback = lastRollback;
        }

        public long Generation { get; }
        public long HighestAcceptedSequence { get; }
        public ReleaseReference Current { get; }
        public ReleaseReference LastKnownGood { get; }
        public PendingActivation Pending { get; }
        public RollbackRecord LastRollback { get; }

        public InstallState WithPending(PendingActivation pending) =>
            new InstallState(
                Generation,
                HighestAcceptedSequence,
                Current,
                LastKnownGood,
                pending,
                LastRollback);
    }

    internal sealed class StartupMarker
    {
        public string ActivationId { get; set; }
        public int ProcessId { get; set; }
    }

    internal sealed class EnvelopeParts
    {
        public byte[] SignedBytes { get; set; }
        public byte[] SignatureBytes { get; set; }
    }
}
