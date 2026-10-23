using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using Malco.Launcher;
using LauncherProgram = Malco.Launcher.Program;
using Xunit;

namespace Malco.ContractTests
{
    public sealed class ReleaseLimitContractTests
    {
        private static readonly string[] RequiredFiles =
        {
            "Malco.exe",
            "Malco.dll",
            "Malco.runtimeconfig.json",
            "Malco.Telemetry.dll",
            "telemetry-policy.json",
            "BwrApi.Client.dll",
            "MALCO-PACKAGE-BOM.json",
            "RUNTIME-CONTRACT.json",
            "LICENSE.txt",
            "bwrapi/runtime/win-x64/bwrapi_runtime.dll",
            "bwrapi/runtime/win-x64/LICENSE.runtime.txt",
            "bwrapi/runtime/win-x64/THIRD_PARTY_NOTICES.md"
        };

        [Theory]
        [InlineData(2000, 10, 0)]
        [InlineData(100, 100, 0)]
        [InlineData(100, 10, 1)]
        public void SignedReleaseBeyondPolicyLimitsIsReportedWithItsSequence(
            long archiveLength, long fileLength, int extraFiles)
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var verifier = new ReleaseVerifier(Policy(key));
            Assert.Equal(7, verifier.VerifyEnvelope(Envelope(key, 7, 100, 10, 0)).Manifest.Sequence);

            var envelope = Envelope(key, 7, archiveLength, fileLength, extraFiles);
            var exceeded = Assert.Throws<ReleaseExceedsPolicyException>(() => verifier.VerifyEnvelope(envelope));
            Assert.Equal(7, exceeded.Sequence);

            // Without a valid signature the same release stays an ordinary feed failure.
            using var otherKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            Assert.Throws<CryptographicException>(
                () => new ReleaseVerifier(Policy(otherKey)).VerifyEnvelope(envelope));
        }

        [Fact]
        public void NewerReleaseBeyondPolicyPointsToInstallerAndLaunchesCurrentVersion()
        {
            var current = new ReleaseReference(3, new string('a', 64));
            var staged = new ReleaseReference(4, new string('b', 64));
            var state = new InstallState(1, 3, current, null, null, null);
            var stagedResult = new LauncherProgram.StartupUpdateResult(
                LauncherProgram.StartupUpdateDisposition.Continue, staged, UpdateRequirement.Optional);

            var notified = 0;
            var result = LauncherProgram.EvaluateStartupUpdate(state, stagedResult, _ => false,
                () => throw new ReleaseExceedsPolicyException(5, "Too large"),
                (_, _, _) => throw new InvalidOperationException("Must not install"),
                () => notified++, out var recheck);
            Assert.Equal(1, notified);
            Assert.False(recheck);
            Assert.Equal(LauncherProgram.StartupUpdateDisposition.Continue, result.Disposition);
            Assert.True(staged.SameAs(result.Candidate));

            LauncherProgram.EvaluateStartupUpdate(state, default, _ => false,
                () => throw new ReleaseExceedsPolicyException(3, "Too large"),
                (_, _, _) => throw new InvalidOperationException("Must not install"),
                () => notified++, out _);
            Assert.Equal(1, notified);
        }

        private static LauncherPolicy Policy(ECDsa key) => new LauncherPolicy
        {
            PublicKeySubjectPublicKeyInfo = key.ExportSubjectPublicKeyInfo(),
            MaximumEnvelopeBytes = 64 * 1024,
            MaximumSignedManifestBytes = 32 * 1024,
            MaximumFiles = RequiredFiles.Length,
            MaximumArchiveBytes = 1000,
            MaximumPayloadBytes = 1000
        };

        private static byte[] Envelope(ECDsa key, long sequence, long archiveLength, long fileLength, int extraFiles)
        {
            var paths = RequiredFiles
                .Concat(Enumerable.Range(0, extraFiles).Select(index => "extra-" + index + ".txt"))
                .OrderBy(path => path, StringComparer.Ordinal);
            var manifest = new Dictionary<string, object>
            {
                ["schema"] = "malco.release.v2",
                ["product"] = "Malco",
                ["platform"] = "win-x64",
                ["sequence"] = sequence,
                ["version"] = "1.0.0",
                ["update_policy"] = "optional",
                ["components"] = new Dictionary<string, object>
                {
                    ["malco_version"] = "1.0.0",
                    ["bwrapi_version"] = "1.0.0"
                },
                ["archive"] = new Dictionary<string, object>
                {
                    ["url"] = "https://example.invalid/payload.zip",
                    ["length"] = archiveLength,
                    ["sha256"] = new string('0', 64)
                },
                ["files"] = paths.Select(path => new Dictionary<string, object>
                {
                    ["path"] = path,
                    ["length"] = fileLength,
                    ["sha256"] = new string('1', 64)
                }).ToArray()
            };
            var signed = JsonSerializer.SerializeToUtf8Bytes(manifest);
            var signature = key.SignData(signed, HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
            {
                writer.WriteStartObject();
                writer.WriteString("schema", "malco.signed-release-envelope.v1");
                writer.WriteString("signed", Convert.ToBase64String(signed));
                writer.WriteString("signature", Convert.ToBase64String(signature));
                writer.WriteEndObject();
            }
            return stream.ToArray();
        }
    }
}
