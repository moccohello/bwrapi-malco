using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using Malco.Launcher;
using Xunit;

namespace Malco.ContractTests
{
    public sealed class LauncherPolicyContractTests
    {
        [Fact]
        public void CurrentV2PolicyParsesItsTwentyOneKeysAndPreservesFeedAndLimits()
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            byte[] publicKey = key.ExportSubjectPublicKeyInfo();
            var properties = CreatePolicyProperties(publicKey);
            Assert.Equal(21, properties.Count);

            LauncherPolicy policy = ContractCodec.ParsePolicy(Serialize(properties));

            Assert.Equal(new Uri("https://update.horangmalco.com/releases/latest.json"), policy.FeedUri);
            Assert.Equal(publicKey, policy.PublicKeySubjectPublicKeyInfo);
            Assert.Equal(8192, policy.MaximumPolicyBytes);
            Assert.Equal(1_000_000, policy.MaximumEnvelopeBytes);
            Assert.Equal(700_000, policy.MaximumSignedManifestBytes);
            Assert.Equal(64 * 1024, policy.MaximumStateBytes);
            Assert.Equal(4 * 1024, policy.MaximumStartupMarkerBytes);
            Assert.Equal(8 * 1024, policy.MaximumStagedUpdateBytes);
            Assert.Equal(32, policy.MaximumFiles);
            Assert.Equal(1_000_000L, policy.MaximumArchiveBytes);
            Assert.Equal(2_000_000L, policy.MaximumPayloadBytes);
            Assert.Equal(64, policy.MaximumArchiveEntries);
            Assert.Equal(15_000, policy.FeedTimeoutMilliseconds);
            Assert.Equal(30_000, policy.ArchiveTimeoutMilliseconds);
            Assert.Equal(5_000, policy.StartupTimeoutMilliseconds);
            Assert.Equal(4_000, policy.AuthorizationTimeoutMilliseconds);
            Assert.Equal(3_000, policy.TerminationTimeoutMilliseconds);
            Assert.Equal(1_000, policy.StartupPollMilliseconds);
            Assert.Equal(250, policy.AuthorizationPollMilliseconds);
            Assert.Equal(750, policy.SelectedReleaseStabilityMilliseconds);
        }

        [Theory]
        [InlineData("launcher_coordination_timeout_ms", 30_000L)]
        [InlineData("retained_staged_update_count", 5L)]
        public void PolicyRejectsEachRemovedInternalKey(string name, long value)
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var properties = CreatePolicyProperties(key.ExportSubjectPublicKeyInfo());
            properties.Add(name, value);

            AssertInvalidPolicy(properties);
        }

        [Fact]
        public void PolicyRejectsBothRemovedInternalKeysTogether()
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var properties = CreatePolicyProperties(key.ExportSubjectPublicKeyInfo());
            properties.Add("launcher_coordination_timeout_ms", 30_000L);
            properties.Add("retained_staged_update_count", 5L);

            AssertInvalidPolicy(properties);
        }

        [Theory]
        [InlineData("feed_url")]
        [InlineData("public_key_spki")]
        public void PolicyRejectsMissingEssentialRemainingProperty(string name)
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var properties = CreatePolicyProperties(key.ExportSubjectPublicKeyInfo());
            Assert.True(properties.Remove(name));

            AssertInvalidPolicy(properties);
        }

        [Theory]
        [InlineData("maximum_envelope_bytes", 600_000L)]
        [InlineData("maximum_payload_bytes", 900_000L)]
        [InlineData("startup_timeout_ms", 500L)]
        [InlineData("authorization_poll_ms", 5_000L)]
        [InlineData("authorization_timeout_ms", 500L)]
        [InlineData("selected_release_stability_ms", 5_000L)]
        public void PolicyRejectsCrossLimitInconsistency(string name, long value)
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var properties = CreatePolicyProperties(key.ExportSubjectPublicKeyInfo());
            properties[name] = value;

            AssertInvalidPolicy(properties);
        }

        [Fact]
        public void CurrentV2PolicyAllowsJsonWhitespaceAndPropertyOrderChanges()
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var properties = CreatePolicyProperties(key.ExportSubjectPublicKeyInfo());
            var reordered = new Dictionary<string, object>();
            foreach (KeyValuePair<string, object> property in properties.Reverse())
            {
                reordered.Add(property.Key, property.Value);
            }
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(reordered, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            LauncherPolicy policy = ContractCodec.ParsePolicy(bytes);

            Assert.Equal(new Uri("https://update.horangmalco.com/releases/latest.json"), policy.FeedUri);
            Assert.Equal(1_000_000L, policy.MaximumArchiveBytes);
            Assert.Equal(2_000_000L, policy.MaximumPayloadBytes);
        }

        private static Dictionary<string, object> CreatePolicyProperties(byte[] publicKey)
        {
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["schema"] = "malco.launcher-policy.v2",
                ["feed_url"] = "https://update.horangmalco.com/releases/latest.json",
                ["public_key_spki"] = Convert.ToBase64String(publicKey),
                ["maximum_policy_bytes"] = 8192L,
                ["maximum_envelope_bytes"] = 1_000_000L,
                ["maximum_signed_manifest_bytes"] = 700_000L,
                ["maximum_state_bytes"] = 64 * 1024L,
                ["maximum_startup_marker_bytes"] = 4 * 1024L,
                ["maximum_staged_update_bytes"] = 8 * 1024L,
                ["maximum_files"] = 32L,
                ["maximum_archive_bytes"] = 1_000_000L,
                ["maximum_payload_bytes"] = 2_000_000L,
                ["maximum_archive_entries"] = 64L,
                ["feed_timeout_ms"] = 15_000L,
                ["archive_timeout_ms"] = 30_000L,
                ["startup_timeout_ms"] = 5_000L,
                ["authorization_timeout_ms"] = 4_000L,
                ["termination_timeout_ms"] = 3_000L,
                ["startup_poll_ms"] = 1_000L,
                ["authorization_poll_ms"] = 250L,
                ["selected_release_stability_ms"] = 750L
            };
        }

        private static byte[] Serialize(Dictionary<string, object> properties) =>
            JsonSerializer.SerializeToUtf8Bytes(properties);

        private static void AssertInvalidPolicy(Dictionary<string, object> properties)
        {
            Assert.Throws<InvalidDataException>(() => ContractCodec.ParsePolicy(Serialize(properties)));
        }
    }
}
