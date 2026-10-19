using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using Malco.Launcher;
using LauncherProgram = Malco.Launcher.Program;
using Xunit;

namespace Malco.ContractTests
{
    public sealed class RollbackPolicyContractTests
    {
        [Fact]
        public void CleanupPreservesSelectorsStagedRollbackAndRunningVersionsWithoutPolicyInjection()
        {
            string root = Path.Combine(Path.GetTempPath(), "malco-retention-contract-" + Guid.NewGuid().ToString("N"));
            try
            {
                var cleaner = new ReleaseStorageCleaner(Path.Combine(root, "versions"), Path.Combine(root, "staging"));
                var references = new ReleaseReference[7];
                for (int index = 0; index < references.Length; index++)
                {
                    references[index] = new ReleaseReference(index + 1, new string("abcdef0"[index], 64));
                    string directory = cleaner.VersionDirectory(references[index]);
                    Directory.CreateDirectory(directory);
                    File.WriteAllText(Path.Combine(directory, "release-envelope.json"), "preserved evidence");
                }
                var state = new InstallState(1, 7, references[0], references[1],
                    new PendingActivation(references[3], references[4], "activation", UpdateRequirement.Optional, true, 0, null, null),
                    new RollbackRecord(references[2], references[0], UpdateRequirement.Required));
                using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
                var verifier = new ReleaseVerifier(new LauncherPolicy
                {
                    PublicKeySubjectPublicKeyInfo = key.ExportSubjectPublicKeyInfo()
                });
                var examined = new List<long>();
                cleaner.CleanupUnreferencedVersions(state, verifier, reference =>
                {
                    examined.Add(reference.Sequence);
                    return reference.Sequence == 7;
                }, references[5]);
                Assert.Equal(new long[] { 7 }, examined);
                foreach (var reference in references)
                {
                    Assert.Equal("preserved evidence", File.ReadAllText(Path.Combine(cleaner.VersionDirectory(reference), "release-envelope.json")));
                }
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [Fact]
        public void RequiredRollbackSurvivesPersistenceCleanupAndOfflineStartup()
        {
            var failed = new ReleaseReference(2, new string('a', 64));
            var previous = new ReleaseReference(1, new string('b', 64));
            var state = new InstallState(1, 2, previous, null, null,
                new RollbackRecord(failed, previous, UpdateRequirement.Required));
            var policy = new LauncherPolicy { MaximumStateBytes = 4096 };
            state = ContractCodec.ParseState(ContractCodec.SerializeState(state), policy);
            Assert.Equal(UpdateRequirement.Required, state.LastRollback.UpdateRequirement);

            string root = Path.Combine(Path.GetTempPath(), "malco-rollback-contract-" + Guid.NewGuid().ToString("N"));
            try
            {
                var cleaner = new ReleaseStorageCleaner(Path.Combine(root, "versions"), Path.Combine(root, "staging"));
                string candidateDirectory = cleaner.VersionDirectory(failed);
                Directory.CreateDirectory(candidateDirectory);
                string evidence = Path.Combine(candidateDirectory, "release-envelope.json");
                File.WriteAllText(evidence, "retained policy evidence");
                using var signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
                policy.PublicKeySubjectPublicKeyInfo = signingKey.ExportSubjectPublicKeyInfo();
                var examined = new List<long>();
                cleaner.CleanupUnreferencedVersions(state, new ReleaseVerifier(policy), reference =>
                {
                    examined.Add(reference.Sequence);
                    return false;
                });
                Assert.DoesNotContain(2L, examined);
                Assert.Equal("retained policy evidence", File.ReadAllText(evidence));

                // After external loss of candidate files, the persisted required
                // policy must still block when the feed is offline.
                File.Delete(evidence);
                var result = LauncherProgram.EvaluateStartupUpdate(state, default,
                    _ => throw new InvalidOperationException("Persisted policy needs no verifier"),
                    () => throw new IOException("Offline"),
                    (_, _, _) => throw new InvalidOperationException("No update to install"), out _);
                Assert.Equal(LauncherProgram.StartupUpdateDisposition.RequiredUpdateFailed, result.Disposition);
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [Fact]
        public void OlderV2RollbackRecordRemainsReadableWithoutInventingOptionalPolicy()
        {
            var failed = new ReleaseReference(2, new string('a', 64));
            var previous = new ReleaseReference(1, new string('b', 64));
            var state = new InstallState(1, 2, previous, null, null, new RollbackRecord(failed, previous));
            var parsed = ContractCodec.ParseState(ContractCodec.SerializeState(state),
                new LauncherPolicy { MaximumStateBytes = 4096 });
            Assert.Null(parsed.LastRollback.UpdateRequirement);
            Assert.True(failed.SameAs(parsed.LastRollback.From));
        }

        [Fact]
        public void NewerVerifiedOptionalStagedCandidateRecoversOfflineAndInheritsRequiredBarrier()
        {
            var candidate = new ReleaseReference(3, new string('c', 64));
            var result = LauncherProgram.EvaluateStartupUpdate(RequiredRollback(),
                new LauncherProgram.StartupUpdateResult(LauncherProgram.StartupUpdateDisposition.Continue,
                    candidate, UpdateRequirement.Optional),
                _ => throw new InvalidOperationException("Persisted policy needs no verifier"),
                () => throw new IOException("Offline"),
                (_, _, _) => throw new InvalidOperationException("No online update"), out _);
            Assert.Equal(LauncherProgram.StartupUpdateDisposition.RequiredActivationPending, result.Disposition);
            Assert.Equal(UpdateRequirement.Required, result.Requirement);
            Assert.True(candidate.SameAs(result.Candidate));
        }

        [Fact]
        public void FailedAcceptedCandidateCannotBeRetriedOffline()
        {
            var state = RequiredRollback();
            var result = LauncherProgram.EvaluateStartupUpdate(state,
                new LauncherProgram.StartupUpdateResult(LauncherProgram.StartupUpdateDisposition.RequiredActivationPending,
                    state.LastRollback.From, UpdateRequirement.Required),
                _ => false, () => throw new IOException("Offline"),
                (_, _, _) => throw new InvalidOperationException("No online update"), out _);
            Assert.Equal(LauncherProgram.StartupUpdateDisposition.RequiredUpdateFailed, result.Disposition);
            Assert.Null(result.Candidate);
        }

        [Fact]
        public void OnlineNewerOptionalReleaseCanRepairRequiredFailure()
        {
            var candidate = new ReleaseReference(3, new string('c', 64));
            bool offered = false;
            var result = LauncherProgram.EvaluateStartupUpdate(RequiredRollback(), default, _ => false,
                () => Latest(3, "optional"),
                (latest, required, fallback) =>
                {
                    offered = true;
                    Assert.Equal(3, latest.Manifest.Sequence);
                    Assert.True(required);
                    return LauncherProgram.ResolveInstalledStartupUpdateResult(
                        new UpdateDialogResult { Accepted = true, Installed = true }, candidate, required, fallback);
                }, out _);
            Assert.True(offered);
            Assert.Equal(LauncherProgram.StartupUpdateDisposition.RequiredActivationPending, result.Disposition);
            Assert.Equal(UpdateRequirement.Required, result.Requirement);
            Assert.True(candidate.SameAs(result.Candidate));
        }

        [Theory]
        [InlineData("cancel")]
        [InlineData("failure")]
        [InlineData("incomplete")]
        public void NewerOptionalReleaseWithoutSuccessfulInstallCannotLiftRequiredBarrier(string outcome)
        {
            var result = LauncherProgram.EvaluateStartupUpdate(RequiredRollback(), default, _ => false,
                () => Latest(3, "optional"),
                (_, required, fallback) => LauncherProgram.ResolveInstalledStartupUpdateResult(
                    new UpdateDialogResult
                    {
                        Accepted = outcome != "cancel",
                        Installed = false,
                        Error = outcome == "failure" ? new IOException("Download failed") : null
                    }, null, required, fallback), out _);
            Assert.Equal(LauncherProgram.StartupUpdateDisposition.RequiredUpdateFailed, result.Disposition);
            Assert.Null(result.Candidate);
        }

        [Fact]
        public void OnlineSameFailedReleaseDoesNotReachInstallation()
        {
            var result = LauncherProgram.EvaluateStartupUpdate(RequiredRollback(), default, _ => false,
                () => Latest(2, "required"),
                (_, _, _) => throw new InvalidOperationException("Must not retry failed release"), out _);
            Assert.Equal(LauncherProgram.StartupUpdateDisposition.RequiredUpdateFailed, result.Disposition);
            Assert.Null(result.Candidate);
        }

        [Fact]
        public void NewerStagedCandidateStillRecoversWhenFeedContainsFailedRequiredSequence()
        {
            var candidate = new ReleaseReference(3, new string('c', 64));
            var result = LauncherProgram.EvaluateStartupUpdate(RequiredRollback(),
                new LauncherProgram.StartupUpdateResult(LauncherProgram.StartupUpdateDisposition.Continue, candidate, UpdateRequirement.Optional),
                _ => false, () => Latest(2, "required"),
                (_, _, _) => throw new InvalidOperationException("Use newer verified staged release"), out _);
            Assert.Equal(LauncherProgram.StartupUpdateDisposition.RequiredActivationPending, result.Disposition);
            Assert.True(candidate.SameAs(result.Candidate));
        }

        private static InstallState RequiredRollback()
        {
            var failed = new ReleaseReference(2, new string('a', 64));
            var previous = new ReleaseReference(1, new string('b', 64));
            return new InstallState(1, 2, previous, null, null,
                new RollbackRecord(failed, previous, UpdateRequirement.Required));
        }

        private static VerifiedEnvelope Latest(long sequence, string requirement) => new VerifiedEnvelope
        {
            Manifest = new ReleaseManifest { Sequence = sequence, UpdatePolicy = requirement }
        };

        [Fact]
        public void UnknownLegacyRollbackPolicyCannotPermitOlderOfflineRelease()
        {
            var state = RequiredRollback();
            state = new InstallState(state.Generation, state.HighestAcceptedSequence,
                state.Current, null, null, new RollbackRecord(state.LastRollback.From, state.Current));
            var result = LauncherProgram.EvaluateStartupUpdate(state, default,
                _ => throw new IOException("Signed evidence missing"),
                () => throw new IOException("Offline"),
                (_, _, _) => throw new InvalidOperationException("No update to install"), out _);
            Assert.Equal(LauncherProgram.StartupUpdateDisposition.RequiredUpdateFailed, result.Disposition);
        }

        [Fact]
        public void OptionalRollbackStillPermitsOlderOfflineRelease()
        {
            var state = RequiredRollback();
            state = new InstallState(state.Generation, state.HighestAcceptedSequence,
                state.Current, null, null, new RollbackRecord(state.LastRollback.From, state.Current, UpdateRequirement.Optional));
            var result = LauncherProgram.EvaluateStartupUpdate(state, default,
                _ => throw new InvalidOperationException("Persisted policy needs no verifier"),
                () => throw new IOException("Offline"),
                (_, _, _) => throw new InvalidOperationException("No update to install"), out _);
            Assert.Equal(LauncherProgram.StartupUpdateDisposition.Continue, result.Disposition);
        }
    }
}
