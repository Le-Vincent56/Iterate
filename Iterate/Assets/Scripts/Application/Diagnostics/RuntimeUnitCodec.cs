using System.Collections.Generic;
using Iterate.Application.Content.Json;
using Iterate.Domain.Compilation;
using Iterate.Domain.Trace;
using Iterate.Domain.Values;

namespace Iterate.Application.Diagnostics
{
    /// <summary>
    /// Reads and writes a <see cref="RuntimeUnitRecord"/> and the two halves it is made of. Three shapes
    /// here are easy to get subtly wrong and are handled explicitly: a lineage is rebuilt by folding
    /// <see cref="EffectOriginLineage.Append"/> from the empty lineage because its constructor is
    /// private; a Structure context is absent as a JSON null rather than as an empty object, since its
    /// own constructor rejects an empty ancestry; and its repeat and condition identities are null when
    /// they do not apply, which is a different state from the empty string the constructor refuses.
    /// </summary>
    internal static class RuntimeUnitCodec
    {
        /// <summary>
        /// Writes a unit record as an object.
        /// </summary>
        /// <param name="unit">The unit to write.</param>
        /// <returns>The object.</returns>
        internal static JsonObject ToJson(RuntimeUnitRecord unit)
        {
            return new JsonObjectBuilder()
                .Add("identity", unit.Identity.Value)
                .Add("opening", OpeningToJson(unit.Opening))
                .Add("closure", ClosureToJson(unit.Closure))
                .Add("childEvents", ExecutionRecordCodec.Events(unit.ChildEvents))
                .Add("descendantUnits", ExecutionRecordCodec.Units(unit.DescendantUnits))
                .Add("startOrder", unit.StartOrder)
                .Add("completionOrder", unit.CompletionOrder)
                .Build();
        }

        /// <summary>
        /// Reads a unit record from its reader, recording any defect on it rather than throwing.
        /// </summary>
        /// <param name="reader">The unit's reader.</param>
        /// <returns>The unit, or null when a defect was recorded.</returns>
        internal static RuntimeUnitRecord FromJson(JsonMemberReader reader)
        {
            int identity = reader.Int("identity");
            RuntimeUnitOpening opening = OpeningFromJson(reader.Child("opening"));
            RuntimeUnitClosure closure = ClosureFromJson(reader.Child("closure"));
            List<TraceEventID> childEvents = ExecutionRecordCodec.ReadEventIDs(reader, "childEvents");
            List<RuntimeUnitID> descendants = ExecutionRecordCodec.ReadUnitIDs(reader, "descendantUnits");
            int startOrder = reader.Int("startOrder");
            int completionOrder = reader.Int("completionOrder");

            if (reader.Failed)
                return null;

            return new RuntimeUnitRecord(
                new RuntimeUnitID(identity),
                opening,
                closure,
                childEvents,
                descendants,
                startOrder,
                completionOrder
            );
        }

        /// <summary>
        /// Writes a unit opening.
        /// </summary>
        /// <param name="opening">The opening to write.</param>
        /// <returns>The object.</returns>
        private static JsonObject OpeningToJson(RuntimeUnitOpening opening)
        {
            return new JsonObjectBuilder()
                .Add("hostInstance", ExecutionRecordCodec.InstanceOrNull(opening.HostInstance))
                .Add("coreLineIdentity", opening.CoreLineIdentity)
                .Add("ownership", opening.Ownership.ToString())
                .Add("position", opening.Position.HasValue
                    ? JsonValues.Integer(opening.Position.Value.LineNumber)
                    : JsonValues.Null())
                .Add("activation", opening.Activation.ToString())
                .Add("originatingRequestIdentity", opening.OriginatingRequestIdentity)
                .Add("addedExecutionDepth", opening.AddedExecutionDepth)
                .Add("lineage", ExecutionRecordCodec.Instances(opening.Lineage.Entries))
                .Add("structureContext", StructureToJson(opening.StructureContext))
                .Add("parentUnit", ExecutionRecordCodec.UnitOrNull(opening.ParentUnit))
                .Build();
        }

        /// <summary>
        /// Reads a unit opening.
        /// </summary>
        /// <param name="reader">The opening's reader.</param>
        /// <returns>The opening, or null when a defect was recorded.</returns>
        private static RuntimeUnitOpening OpeningFromJson(JsonMemberReader reader)
        {
            InstanceID? hostInstance = ExecutionRecordCodec.ReadInstanceOrNull(reader, "hostInstance");
            string coreLineIdentity = reader.OptionalString("coreLineIdentity");
            OwnershipClassification ownership = reader.Enum<OwnershipClassification>("ownership");
            int? position = reader.OptionalInt("position");
            ActivationKind activation = reader.Enum<ActivationKind>("activation");
            string originatingRequestIdentity = reader.OptionalString("originatingRequestIdentity");
            int addedExecutionDepth = reader.Int("addedExecutionDepth");
            List<InstanceID> lineage = ExecutionRecordCodec.ReadInstances(reader, "lineage");
            StructureContext structureContext = StructureFromJson(reader);
            RuntimeUnitID? parentUnit = ExecutionRecordCodec.ReadUnitOrNull(reader, "parentUnit");

            if (reader.Failed)
                return null;

            EffectOriginLineage origin = EffectOriginLineage.Empty;
            for (int index = 0; index < lineage.Count; index++)
            {
                origin = origin.Append(lineage[index]);
            }

            return new RuntimeUnitOpening(
                hostInstance,
                coreLineIdentity,
                ownership,
                position.HasValue ? new SourcePosition(position.Value) : null,
                activation,
                originatingRequestIdentity,
                addedExecutionDepth,
                origin,
                structureContext,
                parentUnit
            );
        }

        /// <summary>
        /// Writes a unit closure.
        /// </summary>
        /// <param name="closure">The closure to write.</param>
        /// <returns>The object.</returns>
        private static JsonObject ClosureToJson(RuntimeUnitClosure closure)
        {
            List<JsonValue> requests = new(closure.CreatedRequestIdentities.Count);
            for (int index = 0; index < closure.CreatedRequestIdentities.Count; index++)
            {
                requests.Add(JsonValues.String(closure.CreatedRequestIdentities[index]));
            }

            return new JsonObjectBuilder()
                .Add("originalDisposition", closure.OriginalDisposition.HasValue
                    ? JsonValues.String(closure.OriginalDisposition.Value.ToString())
                    : JsonValues.Null())
                .Add("finalDisposition", closure.FinalDisposition.ToString())
                .Add("primaryOperationEvent", ExecutionRecordCodec.EventOrNull(closure.PrimaryOperationEvent))
                .Add("createdRequestIdentities", JsonValues.Array(requests))
                .Add("status", closure.Status.ToString())
                .Add("safetyStatus", closure.SafetyStatus.ToString())
                .Build();
        }

        /// <summary>
        /// Reads a unit closure.
        /// </summary>
        /// <param name="reader">The closure's reader.</param>
        /// <returns>The closure, or null when a defect was recorded.</returns>
        private static RuntimeUnitClosure ClosureFromJson(JsonMemberReader reader)
        {
            EventDisposition? originalDisposition = reader.OptionalEnum<EventDisposition>("originalDisposition");
            EventDisposition finalDisposition = reader.Enum<EventDisposition>("finalDisposition");
            TraceEventID? primaryOperationEvent = ExecutionRecordCodec.ReadEventOrNull(reader, "primaryOperationEvent");
            List<string> requests = ReadRequests(reader);
            UnitClosureStatus status = reader.Enum<UnitClosureStatus>("status");
            SafetyStatus safetyStatus = reader.Enum<SafetyStatus>("safetyStatus");

            if (reader.Failed)
                return null;

            return new RuntimeUnitClosure(
                originalDisposition,
                finalDisposition,
                primaryOperationEvent,
                requests,
                status,
                safetyStatus
            );
        }

        /// <summary>
        /// Reads the created-request identities.
        /// </summary>
        /// <param name="reader">The closure's reader.</param>
        /// <returns>The identities, or an empty list when a defect was recorded.</returns>
        private static List<string> ReadRequests(JsonMemberReader reader)
        {
            List<string> requests = new();
            JsonArray items = reader.Array("createdRequestIdentities");
            if (items == null)
                return requests;

            for (int index = 0; index < items.Items.Count; index++)
            {
                if (items.Items[index] is not JsonString text)
                {
                    reader.Fail("createdRequestIdentities", "an entry is not a string.");
                    return requests;
                }

                requests.Add(text.Value);
            }

            return requests;
        }

        /// <summary>
        /// Writes a Structure context, or a null literal when the unit is not inside one.
        /// </summary>
        /// <param name="context">The context, or null.</param>
        /// <returns>The object or the null literal.</returns>
        internal static JsonValue StructureToJson(StructureContext context)
        {
            if (context == null)
                return JsonValues.Null();

            return new JsonObjectBuilder()
                .Add("structureAncestry", ExecutionRecordCodec.Instances(context.StructureAncestry))
                .Add("structureEntryIdentity", context.StructureEntryIdentity)
                .Add("repeatIterationIdentity", context.RepeatIterationIdentity)
                .Add("conditionEvaluationIdentity", context.ConditionEvaluationIdentity)
                .Build();
        }

        /// <summary>
        /// Reads a Structure context, distinguishing an absent member from a null one.
        /// </summary>
        /// <param name="reader">The opening's reader.</param>
        /// <returns>The context, or null when the unit is not inside one.</returns>
        internal static StructureContext StructureFromJson(JsonMemberReader reader)
        {
            JsonObject nested = reader.OptionalObject("structureContext");
            if (nested == null)
                return null;

            JsonMemberReader context = new(nested, "structureContext");
            List<InstanceID> ancestry = ExecutionRecordCodec.ReadInstances(context, "structureAncestry");
            string entryIdentity = context.String("structureEntryIdentity");
            string repeatIdentity = context.OptionalString("repeatIterationIdentity");
            string conditionIdentity = context.OptionalString("conditionEvaluationIdentity");
            if (ExecutionRecordCodec.Propagate(reader, "structureContext", context))
                return null;

            return new StructureContext(ancestry, entryIdentity, repeatIdentity, conditionIdentity);
        }
    }
}