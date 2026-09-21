using System.Collections.Generic;
using Iterate.Application.Content.Json;
using Iterate.Domain.Compilation;
using Iterate.Domain.Trace;
using Iterate.Domain.Values;

namespace Iterate.Application.Diagnostics
{
    /// <summary>
    /// Reads and writes an <see cref="EventRecord"/> and the twenty-one-member evidence inside it. Most
    /// of those members are nullable and most events leave most of them null, so the codec writes JSON
    /// nulls rather than omitting members: an absent member is a defect everywhere else in this format,
    /// and an event that simply had no target should not read as a malformed one.
    /// </summary>
    internal static class EventEvidenceCodec
    {
        /// <summary>
        /// Writes an event record as an object.
        /// </summary>
        /// <param name="record">The event to write.</param>
        /// <returns>The object.</returns>
        internal static JsonObject ToJson(EventRecord record)
        {
            EventEvidence evidence = record.Evidence;
            List<JsonValue> qualifiers = new(evidence.Qualifiers.Count);
            for (int index = 0; index < evidence.Qualifiers.Count; index++)
            {
                qualifiers.Add(JsonValues.String(evidence.Qualifiers[index]));
            }

            JsonObject body = new JsonObjectBuilder()
                .Add("family", evidence.Family)
                .Add("subtype", evidence.Subtype)
                .Add("qualifiers", JsonValues.Array(qualifiers))
                .Add("causalDepth", evidence.CausalDepth)
                .Add("containingUnit", ExecutionRecordCodec.UnitOrNull(evidence.ContainingUnit))
                .Add("parentEvent", ExecutionRecordCodec.EventOrNull(evidence.ParentEvent))
                .Add("causingEvent", ExecutionRecordCodec.EventOrNull(evidence.CausingEvent))
                .Add("hostInstance", ExecutionRecordCodec.InstanceOrNull(evidence.HostInstance))
                .Add("coreLineIdentity", evidence.CoreLineIdentity)
                .Add("effectOriginInstance", ExecutionRecordCodec.InstanceOrNull(evidence.EffectOriginInstance))
                .Add("ownership", evidence.Ownership.HasValue
                    ? JsonValues.String(evidence.Ownership.Value.ToString())
                    : JsonValues.Null())
                .Add("position", evidence.Position.HasValue
                    ? JsonValues.Integer(evidence.Position.Value.LineNumber)
                    : JsonValues.Null())
                .Add("targetIdentity", evidence.TargetIdentity)
                .Add("lineage", ExecutionRecordCodec.Instances(evidence.Lineage.Entries))
                .Add("addedExecutionRequestIdentity", evidence.AddedExecutionRequestIdentity)
                .Add("addedExecutionDepth", evidence.AddedExecutionDepth)
                .Add("structureContext", RuntimeUnitCodec.StructureToJson(evidence.StructureContext))
                .Add("disposition", evidence.Disposition.HasValue
                    ? JsonValues.String(evidence.Disposition.Value.ToString())
                    : JsonValues.Null())
                .Add("dispositionReason", evidence.DispositionReason)
                .Add("safetyStatus", evidence.SafetyStatus.ToString())
                .Add("payload", EventPayloadCodec.ToJson(evidence.Payload))
                .Build();

            return new JsonObjectBuilder()
                .Add("identity", record.Identity.Value)
                .Add("evidence", body)
                .Build();
        }

        /// <summary>
        /// Reads an event record from its reader, recording any defect on it rather than throwing.
        /// </summary>
        /// <param name="reader">The event's reader.</param>
        /// <returns>The event, or the default when a defect was recorded.</returns>
        internal static EventRecord FromJson(JsonMemberReader reader)
        {
            int identity = reader.Int("identity");
            JsonMemberReader evidence = reader.Child("evidence");

            string family = evidence.String("family");
            string subtype = evidence.String("subtype");
            List<string> qualifiers = ReadQualifiers(evidence);
            int causalDepth = evidence.Int("causalDepth");
            RuntimeUnitID? containingUnit = ExecutionRecordCodec.ReadUnitOrNull(evidence, "containingUnit");
            TraceEventID? parentEvent = ExecutionRecordCodec.ReadEventOrNull(evidence, "parentEvent");
            TraceEventID? causingEvent = ExecutionRecordCodec.ReadEventOrNull(evidence, "causingEvent");
            InstanceID? hostInstance = ExecutionRecordCodec.ReadInstanceOrNull(evidence, "hostInstance");
            string coreLineIdentity = evidence.OptionalString("coreLineIdentity");
            InstanceID? effectOrigin = ExecutionRecordCodec.ReadInstanceOrNull(evidence, "effectOriginInstance");
            OwnershipClassification? ownership = evidence.OptionalEnum<OwnershipClassification>("ownership");
            int? position = evidence.OptionalInt("position");
            string targetIdentity = evidence.OptionalString("targetIdentity");
            List<InstanceID> lineage = ExecutionRecordCodec.ReadInstances(evidence, "lineage");
            string addedExecutionRequestIdentity = evidence.OptionalString("addedExecutionRequestIdentity");
            int addedExecutionDepth = evidence.Int("addedExecutionDepth");
            StructureContext structureContext = RuntimeUnitCodec.StructureFromJson(evidence);
            EventDisposition? disposition = evidence.OptionalEnum<EventDisposition>("disposition");
            string dispositionReason = evidence.OptionalString("dispositionReason");
            SafetyStatus safetyStatus = evidence.Enum<SafetyStatus>("safetyStatus");
            EventPayload payload = EventPayloadCodec.FromJson(evidence, "payload");

            if (reader.Failed)
                return default;

            EffectOriginLineage origin = EffectOriginLineage.Empty;
            for (int index = 0; index < lineage.Count; index++)
            {
                origin = origin.Append(lineage[index]);
            }

            return new EventRecord(new TraceEventID(identity), new EventEvidence(
                family,
                subtype,
                qualifiers,
                causalDepth,
                containingUnit,
                parentEvent,
                causingEvent,
                hostInstance,
                coreLineIdentity,
                effectOrigin,
                ownership,
                position.HasValue ? new SourcePosition(position.Value) : null,
                targetIdentity,
                origin,
                addedExecutionRequestIdentity,
                addedExecutionDepth,
                structureContext,
                disposition,
                dispositionReason,
                safetyStatus,
                payload
            ));
        }

        /// <summary>
        /// Reads the event's qualifiers.
        /// </summary>
        /// <param name="evidence">The evidence reader.</param>
        /// <returns>The qualifiers, or an empty list when a defect was recorded.</returns>
        private static List<string> ReadQualifiers(JsonMemberReader evidence)
        {
            List<string> qualifiers = new();
            JsonArray items = evidence.Array("qualifiers");
            if (items == null)
                return qualifiers;

            for (int index = 0; index < items.Items.Count; index++)
            {
                if (items.Items[index] is not JsonString text)
                {
                    evidence.Fail("qualifiers", "an entry is not a string.");
                    return qualifiers;
                }

                qualifiers.Add(text.Value);
            }

            return qualifiers;
        }
    }
}