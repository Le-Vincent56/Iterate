using System.Collections.Generic;
using Iterate.Application.Content.Json;
using Iterate.Domain.Determinism;
using Iterate.Domain.Trace;

namespace Iterate.Application.Diagnostics
{
    /// <summary>
    /// Reads and writes an <see cref="EventPayload"/> as a <c>kind</c>-discriminated object, or a JSON
    /// null for the many events that carry none. An unrecognised kind is a defect rather than a silently
    /// dropped payload: a document from a build that added a fourth kind must refuse to load rather than
    /// load as three-quarters of itself.
    /// </summary>
    internal static class EventPayloadCodec
    {
        private const string QuantityChange = "QuantityChange";
        private const string RandomDecision = "RandomDecision";
        private const string SafetyAbort = "SafetyAbort";

        /// <summary>
        /// Writes a payload, or a null literal when the event carries none.
        /// </summary>
        /// <param name="payload">The payload, or null.</param>
        /// <returns>The object or the null literal.</returns>
        internal static JsonValue ToJson(EventPayload payload)
        {
            if (payload == null)
                return JsonValues.Null();

            if (payload is QuantityChangePayload quantity)
                return QuantityToJson(quantity);

            if (payload is RandomDecisionPayload decision)
            {
                return new JsonObjectBuilder()
                    .Add("kind", RandomDecision)
                    .Add("record", RandomDecisionRecordCodec.ToJson(decision.Record))
                    .Build();
            }

            SafetyAbortPayload abort = (SafetyAbortPayload)payload;
            List<JsonValue> limits = new(abort.BreachedLimits.Count);
            for (int index = 0; index < abort.BreachedLimits.Count; index++)
            {
                BreachedLimit limit = abort.BreachedLimits[index];
                limits.Add(new JsonObjectBuilder()
                    .Add("limitName", limit.LimitName)
                    .Add("ceiling", limit.Ceiling)
                    .Add("countAtBreach", limit.CountAtBreach)
                    .Build());
            }

            return new JsonObjectBuilder()
                .Add("kind", SafetyAbort)
                .Add("overLimitOccurrenceIdentity", abort.OverLimitOccurrenceIdentity)
                .Add("affectedUnit", abort.AffectedUnit.Value)
                .Add("breachedLimits", JsonValues.Array(limits))
                .Build();
        }

        /// <summary>
        /// Reads a payload from a member that may be a null literal.
        /// </summary>
        /// <param name="parent">The evidence reader.</param>
        /// <param name="key">The payload member's name.</param>
        /// <returns>The payload, or null.</returns>
        internal static EventPayload FromJson(JsonMemberReader parent, string key)
        {
            JsonObject nested = parent.OptionalObject(key);
            if (nested == null)
                return null;

            JsonMemberReader reader = new(nested, key);
            string kind = reader.String("kind");
            if (reader.Failed)
            {
                ExecutionRecordCodec.Propagate(parent, key, reader);
                return null;
            }

            EventPayload payload = ReadKind(reader, kind);
            ExecutionRecordCodec.Propagate(parent, key, reader);
            return payload;
        }

        /// <summary>
        /// Reads the payload body for a discriminated kind.
        /// </summary>
        /// <param name="reader">The payload's reader.</param>
        /// <param name="kind">The kind read from the document.</param>
        /// <returns>The payload, or null when a defect was recorded.</returns>
        private static EventPayload ReadKind(JsonMemberReader reader, string kind)
        {
            if (kind == QuantityChange)
                return QuantityFromJson(reader);

            if (kind == RandomDecision)
            {
                RandomDecisionRecord record = RandomDecisionRecordCodec.FromJson(reader.Child("record"));
                return reader.Failed ? null : new RandomDecisionPayload(record);
            }

            if (kind == SafetyAbort)
                return AbortFromJson(reader);

            reader.Fail("kind", $"'{kind}' is not a payload kind this build reads.");
            return null;
        }

        /// <summary>
        /// Writes a quantity-change payload.
        /// </summary>
        /// <param name="quantity">The payload to write.</param>
        /// <returns>The object.</returns>
        private static JsonObject QuantityToJson(QuantityChangePayload quantity)
        {
            List<JsonValue> modifiers = new(quantity.AppliedModifiers.Count);
            for (int index = 0; index < quantity.AppliedModifiers.Count; index++)
            {
                QuantityModifierEvidence modifier = quantity.AppliedModifiers[index];
                modifiers.Add(new JsonObjectBuilder()
                    .Add("modifierIdentity", modifier.ModifierIdentity)
                    .Add("stage", modifier.Stage)
                    .Add("amount", modifier.Amount)
                    .Build());
            }

            JsonValue bounds = quantity.AppliedBounds == null
                ? JsonValues.Null()
                : new JsonObjectBuilder()
                    .Add("minimum", quantity.AppliedBounds.Minimum.HasValue
                        ? JsonValues.Integer(quantity.AppliedBounds.Minimum.Value)
                        : JsonValues.Null())
                    .Add("maximum", quantity.AppliedBounds.Maximum.HasValue
                        ? JsonValues.Integer(quantity.AppliedBounds.Maximum.Value)
                        : JsonValues.Null())
                    .Build();

            return new JsonObjectBuilder()
                .Add("kind", QuantityChange)
                .Add("quantityIdentity", quantity.QuantityIdentity)
                .Add("category", quantity.Category.ToString())
                .Add("operation", quantity.Operation.ToString())
                .Add("requestedAmount", quantity.RequestedAmount)
                .Add("priorValue", quantity.PriorValue)
                .Add("replacementLink", ExecutionRecordCodec.EventOrNull(quantity.ReplacementLink))
                .Add("appliedModifiers", JsonValues.Array(modifiers))
                .Add("appliedBounds", bounds)
                .Add("finalDelta", quantity.FinalDelta)
                .Add("finalValue", quantity.FinalValue)
                .Build();
        }

        /// <summary>
        /// Reads a quantity-change payload.
        /// </summary>
        /// <param name="reader">The payload's reader.</param>
        /// <returns>The payload, or null when a defect was recorded.</returns>
        private static EventPayload QuantityFromJson(JsonMemberReader reader)
        {
            string quantityIdentity = reader.String("quantityIdentity");
            QuantityCategory category = reader.Enum<QuantityCategory>("category");
            QuantityOperationType operation = reader.Enum<QuantityOperationType>("operation");
            int requestedAmount = reader.Int("requestedAmount");
            int priorValue = reader.Int("priorValue");
            TraceEventID? replacementLink = ExecutionRecordCodec.ReadEventOrNull(reader, "replacementLink");
            List<QuantityModifierEvidence> modifiers = ReadModifiers(reader);
            QuantityBoundsEvidence bounds = ReadBounds(reader);
            int finalDelta = reader.Int("finalDelta");
            int finalValue = reader.Int("finalValue");

            if (reader.Failed)
                return null;

            return new QuantityChangePayload(
                quantityIdentity,
                category,
                operation,
                requestedAmount,
                priorValue,
                replacementLink,
                modifiers,
                bounds,
                finalDelta,
                finalValue
            );
        }

        /// <summary>
        /// Reads the applied modifiers.
        /// </summary>
        /// <param name="reader">The payload's reader.</param>
        /// <returns>The modifiers, or an empty list when a defect was recorded.</returns>
        private static List<QuantityModifierEvidence> ReadModifiers(JsonMemberReader reader)
        {
            List<QuantityModifierEvidence> modifiers = new();
            JsonArray items = reader.Array("appliedModifiers");
            if (items == null)
                return modifiers;

            for (int index = 0; index < items.Items.Count; index++)
            {
                JsonMemberReader element = ExecutionRecordCodec.Element(reader, "appliedModifiers", items, index);
                if (element == null)
                    return modifiers;

                string identity = element.String("modifierIdentity");
                string stage = element.String("stage");
                int amount = element.Int("amount");
                if (ExecutionRecordCodec.Propagate(reader, "appliedModifiers", element))
                    return modifiers;

                modifiers.Add(new QuantityModifierEvidence(identity, stage, amount));
            }

            return modifiers;
        }

        /// <summary>
        /// Reads the applied bounds, which are absent as a null member rather than as an empty object
        /// because the bounds record itself rejects having neither bound.
        /// </summary>
        /// <param name="reader">The payload's reader.</param>
        /// <returns>The bounds, or null.</returns>
        private static QuantityBoundsEvidence ReadBounds(JsonMemberReader reader)
        {
            JsonObject nested = reader.OptionalObject("appliedBounds");
            if (nested == null)
                return null;

            JsonMemberReader bounds = new(nested, "appliedBounds");
            int? minimum = bounds.OptionalInt("minimum");
            int? maximum = bounds.OptionalInt("maximum");
            if (ExecutionRecordCodec.Propagate(reader, "appliedBounds", bounds))
                return null;

            if (minimum == null && maximum == null)
            {
                reader.Fail("appliedBounds", "a bounds evidence requires at least one bound.");
                return null;
            }

            return new QuantityBoundsEvidence(minimum, maximum);
        }

        /// <summary>
        /// Reads a safety-abort payload.
        /// </summary>
        /// <param name="reader">The payload's reader.</param>
        /// <returns>The payload, or null when a defect was recorded.</returns>
        private static EventPayload AbortFromJson(JsonMemberReader reader)
        {
            string occurrence = reader.String("overLimitOccurrenceIdentity");
            int affectedUnit = reader.Int("affectedUnit");
            List<BreachedLimit> limits = new();
            JsonArray items = reader.Array("breachedLimits");
            if (items != null)
            {
                for (int index = 0; index < items.Items.Count; index++)
                {
                    JsonMemberReader element = ExecutionRecordCodec.Element(reader, "breachedLimits", items, index);
                    if (element == null)
                        break;

                    string limitName = element.String("limitName");
                    int ceiling = element.Int("ceiling");
                    int countAtBreach = element.Int("countAtBreach");
                    if (ExecutionRecordCodec.Propagate(reader, "breachedLimits", element))
                        break;

                    limits.Add(new BreachedLimit(limitName, ceiling, countAtBreach));
                }
            }

            if (reader.Failed)
                return null;

            if (limits.Count == 0)
            {
                reader.Fail("breachedLimits", "a safety-abort payload requires at least one breached limit.");
                return null;
            }

            return new SafetyAbortPayload(occurrence, new RuntimeUnitID(affectedUnit), limits);
        }
    }
}