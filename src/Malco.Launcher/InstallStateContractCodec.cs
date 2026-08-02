using System;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Malco.Launcher
{
    internal static partial class ContractCodec
    {
        public static InstallState ParseState(byte[] bytes, LauncherPolicy policy)
        {
            if (policy == null) throw new ArgumentNullException(nameof(policy));
            RequireBounded(bytes, policy.MaximumStateBytes, "install state");
            using (var document = ParseDocument(bytes, 16))
            {
                var root = RequireObject(document.RootElement, "install state");
                RequireProperties(
                    root,
                    "schema",
                    "generation",
                    "highest_accepted_sequence",
                    "current",
                    "last_known_good",
                    "pending",
                    "last_rollback");
                RequireExactString(root, "schema", InstallState.SchemaName);
                var state = new InstallState(
                    RequireInt64(root, "generation", 0, long.MaxValue),
                    RequireInt64(root, "highest_accepted_sequence", 0, long.MaxValue),
                    ParseNullableReference(RequireProperty(root, "current"), "current"),
                    ParseNullableReference(RequireProperty(root, "last_known_good"), "last_known_good"),
                    ParsePending(RequireProperty(root, "pending")),
                    ParseRollback(RequireProperty(root, "last_rollback")));
                ValidateState(state);
                return state;
            }
        }

        public static byte[] SerializeState(InstallState state)
        {
            ValidateState(state);
            using (var stream = new MemoryStream())
            {
                using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
                {
                    writer.WriteStartObject();
                    writer.WriteString("schema", InstallState.SchemaName);
                    writer.WriteNumber("generation", state.Generation);
                    writer.WriteNumber("highest_accepted_sequence", state.HighestAcceptedSequence);
                    WriteReference(writer, "current", state.Current);
                    WriteReference(writer, "last_known_good", state.LastKnownGood);
                    writer.WritePropertyName("pending");
                    if (state.Pending == null)
                    {
                        writer.WriteNullValue();
                    }
                    else
                    {
                        writer.WriteStartObject();
                        WriteReference(writer, "candidate", state.Pending.Candidate);
                        WriteReference(writer, "previous_current", state.Pending.PreviousCurrent);
                        writer.WriteString("activation_id", state.Pending.ActivationId);
                        writer.WriteString(
                            "update_requirement",
                            state.Pending.UpdateRequirement == UpdateRequirement.Required
                                ? "required"
                                : "optional");
                        writer.WriteBoolean("rollback_available", state.Pending.RollbackAvailable);
                        writer.WriteNumber("startup_attempts", state.Pending.StartupAttempts);
                        writer.WritePropertyName("process_id");
                        if (state.Pending.ProcessId.HasValue) writer.WriteNumberValue(state.Pending.ProcessId.Value);
                        else writer.WriteNullValue();
                        writer.WritePropertyName("process_start_time_utc_ticks");
                        if (state.Pending.ProcessStartTimeUtcTicks.HasValue) writer.WriteNumberValue(state.Pending.ProcessStartTimeUtcTicks.Value);
                        else writer.WriteNullValue();
                        writer.WriteEndObject();
                    }
                    writer.WritePropertyName("last_rollback");
                    if (state.LastRollback == null)
                    {
                        writer.WriteNullValue();
                    }
                    else
                    {
                        writer.WriteStartObject();
                        WriteReference(writer, "from", state.LastRollback.From);
                        WriteReference(writer, "to", state.LastRollback.To);
                        writer.WriteEndObject();
                    }
                    writer.WriteEndObject();
                }
                return stream.ToArray();
            }
        }

        public static StartupMarker ParseStartupMarker(byte[] bytes, LauncherPolicy policy)
        {
            if (policy == null) throw new ArgumentNullException(nameof(policy));
            RequireBounded(bytes, policy.MaximumStartupMarkerBytes, "startup marker");
            using (var document = ParseDocument(bytes, 4))
            {
                var root = RequireObject(document.RootElement, "startup marker");
                RequireProperties(root, "schema", "activation_id", "process_id");
                RequireExactString(root, "schema", "malco.startup-marker.v1");
                var activation = RequireString(root, "activation_id", 64);
                if (!ActivationId.IsMatch(activation))
                {
                    throw new InvalidDataException("The startup marker activation ID is invalid.");
                }
                return new StartupMarker
                {
                    ActivationId = activation,
                    ProcessId = checked((int)RequireInt64(root, "process_id", 1, int.MaxValue))
                };
            }
        }

        private static PendingActivation ParsePending(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Null) return null;
            var value = RequireObject(element, "pending activation");
            RequireProperties(value, "candidate", "previous_current", "activation_id", "update_requirement", "rollback_available", "startup_attempts", "process_id", "process_start_time_utc_ticks");
            var activation = RequireString(value, "activation_id", 64);
            if (!ActivationId.IsMatch(activation))
            {
                throw new InvalidDataException("The pending activation ID is invalid.");
            }
            var rollbackProperty = RequireProperty(value, "rollback_available");
            if (rollbackProperty.ValueKind != JsonValueKind.True && rollbackProperty.ValueKind != JsonValueKind.False)
            {
                throw new InvalidDataException("The pending rollback flag must be boolean.");
            }
            return new PendingActivation(
                ParseRequiredReference(RequireProperty(value, "candidate"), "pending candidate"),
                ParseNullableReference(RequireProperty(value, "previous_current"), "previous current"),
                activation,
                ParseUpdateRequirement(value),
                rollbackProperty.GetBoolean(),
                checked((int)RequireInt64(value, "startup_attempts", 0, 2)),
                ParseNullableInt32(RequireProperty(value, "process_id"), "pending process ID"),
                ParseNullableInt64(RequireProperty(value, "process_start_time_utc_ticks"), "pending process start time"));
        }

        private static UpdateRequirement ParseUpdateRequirement(JsonElement value)
        {
            var requirement = RequireString(value, "update_requirement", 8);
            if (string.Equals(requirement, "required", StringComparison.Ordinal))
            {
                return UpdateRequirement.Required;
            }
            if (string.Equals(requirement, "optional", StringComparison.Ordinal))
            {
                return UpdateRequirement.Optional;
            }
            throw new InvalidDataException("The update requirement is not supported.");
        }

        private static RollbackRecord ParseRollback(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Null) return null;
            var value = RequireObject(element, "last rollback");
            RequireProperties(value, "from", "to");
            return new RollbackRecord(
                ParseRequiredReference(RequireProperty(value, "from"), "rollback source"),
                ParseNullableReference(RequireProperty(value, "to"), "rollback target"));
        }

        private static ReleaseReference ParseNullableReference(JsonElement element, string label) =>
            element.ValueKind == JsonValueKind.Null ? null : ParseRequiredReference(element, label);

        private static ReleaseReference ParseRequiredReference(JsonElement element, string label)
        {
            var value = RequireObject(element, label);
            RequireProperties(value, "sequence", "manifest_sha256");
            return new ReleaseReference(
                RequireInt64(value, "sequence", 1, long.MaxValue),
                RequireSha256(value, "manifest_sha256"));
        }

        private static void ValidateState(InstallState state)
        {
            if (state == null || state.Generation < 0 || state.HighestAcceptedSequence < 0)
            {
                throw new InvalidDataException("The install state counters are invalid.");
            }
            ValidateReference(state.Current, state.HighestAcceptedSequence, "current");
            ValidateReference(state.LastKnownGood, state.HighestAcceptedSequence, "last-known-good");
            if (state.Current != null && state.LastKnownGood != null && state.Current.SameAs(state.LastKnownGood))
            {
                throw new InvalidDataException("Current and last-known-good must be distinct releases.");
            }
            if (state.Pending != null)
            {
                if (state.Pending.Candidate == null)
                {
                    throw new InvalidDataException("The pending candidate is missing.");
                }
                if (state.Pending.UpdateRequirement != UpdateRequirement.Optional &&
                    state.Pending.UpdateRequirement != UpdateRequirement.Required)
                {
                    throw new InvalidDataException("The pending update requirement is invalid.");
                }
                ValidateReference(state.Pending.Candidate, state.HighestAcceptedSequence, "pending candidate");
                ValidateReference(state.Pending.PreviousCurrent, state.HighestAcceptedSequence, "pending previous current");
                if (state.Current == null || !state.Current.SameAs(state.Pending.Candidate) ||
                    state.Pending.Candidate.Sequence != state.HighestAcceptedSequence ||
                    !IsActivationId(state.Pending.ActivationId) ||
                    state.Pending.StartupAttempts < 0 || state.Pending.StartupAttempts > 2 ||
                    state.Pending.ProcessId.HasValue != state.Pending.ProcessStartTimeUtcTicks.HasValue ||
                    (state.Pending.ProcessId.HasValue && state.Pending.StartupAttempts == 0) ||
                    state.Pending.RollbackAvailable != (state.Pending.PreviousCurrent != null) ||
                    !ReferencesEqual(state.LastKnownGood, state.Pending.PreviousCurrent))
                {
                    throw new InvalidDataException("The pending activation does not match the atomic selector state.");
                }
            }
            if (state.LastRollback != null)
            {
                if (state.LastRollback.From == null)
                {
                    throw new InvalidDataException("The rollback source is missing.");
                }
                ValidateReference(state.LastRollback.From, state.HighestAcceptedSequence, "rollback source");
                ValidateReference(state.LastRollback.To, state.HighestAcceptedSequence, "rollback target");
            }
            if (state.HighestAcceptedSequence > 0 && !new[]
                {
                    state.Current,
                    state.LastKnownGood,
                    state.Pending?.Candidate,
                    state.Pending?.PreviousCurrent,
                    state.LastRollback?.From,
                    state.LastRollback?.To
                }.Any(reference => reference != null &&
                    reference.Sequence == state.HighestAcceptedSequence))
            {
                throw new InvalidDataException("The monotonic watermark has no accepted release identity.");
            }
        }

        private static void ValidateReference(ReleaseReference reference, long highest, string label)
        {
            if (reference == null) return;
            if (reference.Sequence <= 0 || reference.Sequence > highest || !IsLowerSha256(reference.ManifestSha256))
            {
                throw new InvalidDataException("The " + label + " release reference is invalid.");
            }
        }

        private static int? ParseNullableInt32(JsonElement value, string label)
        {
            if (value.ValueKind == JsonValueKind.Null) return null;
            int result;
            if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out result) || result <= 0)
            {
                throw new InvalidDataException("The " + label + " is invalid.");
            }
            return result;
        }

        private static long? ParseNullableInt64(JsonElement value, string label)
        {
            if (value.ValueKind == JsonValueKind.Null) return null;
            long result;
            if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out result) || result <= 0)
            {
                throw new InvalidDataException("The " + label + " is invalid.");
            }
            return result;
        }

        private static bool ReferencesEqual(ReleaseReference left, ReleaseReference right) =>
            left == null ? right == null : left.SameAs(right);

        private static void WriteReference(Utf8JsonWriter writer, string name, ReleaseReference reference)
        {
            writer.WritePropertyName(name);
            if (reference == null)
            {
                writer.WriteNullValue();
                return;
            }
            writer.WriteStartObject();
            writer.WriteNumber("sequence", reference.Sequence);
            writer.WriteString("manifest_sha256", reference.ManifestSha256);
            writer.WriteEndObject();
        }
    }
}
