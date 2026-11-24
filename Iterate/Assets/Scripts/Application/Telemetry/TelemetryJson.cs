using System.Collections.Generic;
using Iterate.Application.Content.Json;
using Iterate.Application.SessionFlow;
using Iterate.Domain.Compilation;
using Iterate.Domain.Execution;
using Iterate.Domain.Progression;
using Iterate.Domain.Trace;

namespace Iterate.Application.Telemetry
{
    /// <summary>
    /// Builds the data members of the recorder's snapshot, compilation and execution lines. Only
    /// counts, outcomes and data identities are projected, never random-decision evidence or any
    /// seed or Session identity.
    /// </summary>
    internal static class TelemetryJson
    {
        /// <summary>
        /// A configuration snapshot's data.
        /// </summary>
        /// <param name="snapshot">The snapshot.</param>
        /// <returns>Its lines, Patches, pragmas and state against the compiled arrangement.</returns>
        internal static JsonObject Configuration(ConfigurationSnapshot snapshot)
        {
            List<JsonValue> lines = new(snapshot.Lines.Count);
            for (int index = 0; index < snapshot.Lines.Count; index++)
            {
                ConfigurationLine line = snapshot.Lines[index];
                lines.Add(new JsonObjectBuilder()
                    .Add("line", line.Line)
                    .Add("kind", line.Kind.ToString())
                    .Add("item", line.Item.HasValue ? JsonValues.Integer(line.Item.Value.Value) : JsonValues.Null())
                    .Add("definition", line.DefinitionID)
                    .Add("structure", line.Structure.HasValue ? JsonValues.Integer(line.Structure.Value.Value) : JsonValues.Null())
                    .Build());
            }

            List<JsonValue> patches = new(snapshot.Patches.Count);
            for (int index = 0; index < snapshot.Patches.Count; index++)
            {
                ConfigurationPatch patch = snapshot.Patches[index];
                patches.Add(new JsonObjectBuilder()
                    .Add("patch", patch.Patch.Value)
                    .Add("definition", patch.DefinitionID)
                    .Add("host", patch.Host.Value)
                    .Add("socket", patch.Socket)
                    .Build());
            }

            List<JsonValue> pragmas = new(snapshot.Pragmas.Count);
            for (int index = 0; index < snapshot.Pragmas.Count; index++)
            {
                ConfigurationPragma pragma = snapshot.Pragmas[index];
                pragmas.Add(new JsonObjectBuilder()
                    .Add("directive", pragma.Directive.Value)
                    .Add("definition", pragma.DefinitionID)
                    .Build());
            }

            return new JsonObjectBuilder()
                .Add("lines", JsonValues.Array(lines))
                .Add("patches", JsonValues.Array(patches))
                .Add("pragmas", JsonValues.Array(pragmas))
                .Add("state", snapshot.State.ToString())
                .Build();
        }

        /// <summary>
        /// Adds a Buffer snapshot's members to a data builder.
        /// </summary>
        /// <param name="builder">The data builder.</param>
        /// <param name="buffer">The Buffer snapshot, or null when no Process is open.</param>
        /// <returns>The builder.</returns>
        internal static JsonObjectBuilder AddBuffer(JsonObjectBuilder builder, BufferSnapshot buffer)
        {
            if (buffer == null)
                return builder.AddNull("capacity").AddNull("slots").AddNull("incoming").AddNull("exposed").AddNull("pendingArrivals");

            List<JsonValue> slots = new(buffer.Slots.Count);
            for (int index = 0; index < buffer.Slots.Count; index++)
            {
                BufferSlotView slot = buffer.Slots[index];
                slots.Add(new JsonObjectBuilder()
                    .Add("slot", slot.Number)
                    .Add("item", slot.Item.HasValue ? JsonValues.Integer(slot.Item.Value.Value) : JsonValues.Null())
                    .Add("definition", slot.DefinitionID)
                    .Build());
            }

            List<JsonValue> exposed = new(buffer.Exposed.Count);
            for (int index = 0; index < buffer.Exposed.Count; index++)
                exposed.Add(JsonValues.Integer(buffer.Exposed[index].Value));

            return builder
                .Add("capacity", buffer.Capacity)
                .Add("slots", JsonValues.Array(slots))
                .Add("incoming", buffer.Incoming.HasValue ? JsonValues.Integer(buffer.Incoming.Value.Value) : JsonValues.Null())
                .Add("exposed", JsonValues.Array(exposed))
                .Add("pendingArrivals", buffer.PendingArrivals);
        }

        /// <summary>
        /// A compilation attempt's data. Bytes after is computed from the attempt, not read from the
        /// Process, which a final execution can already have closed.
        /// </summary>
        /// <param name="attempt">The attempt.</param>
        /// <returns>Whether it was blocked and why, its classification, costs, and Bytes before and after.</returns>
        internal static JsonObject Compilation(CompilationAttempt attempt)
        {
            CompilationCostBreakdown breakdown = attempt.Breakdown;
            int before = attempt.AvailableBytes.Value;
            return new JsonObjectBuilder()
                .Add("blocked", !attempt.Committed)
                .Add("blockReason", attempt.BlockReason == CompilationBlockReason.None ? null : attempt.BlockReason.ToString())
                .Add("classification", breakdown.Classification.ToString())
                .Add("baseCost", breakdown.BaseCost)
                .Add("finalCost", breakdown.FinalCost)
                .Add("editedCompilation", breakdown.ProgressionIndex)
                .Add("bytesBefore", before)
                .Add("bytesAfter", attempt.Committed ? before - breakdown.FinalCost : before)
                .Build();
        }

        /// <summary>
        /// A finished execution's data.
        /// </summary>
        /// <param name="outcome">The execution's outcome.</param>
        /// <param name="configuration">The configuration identity executed.</param>
        /// <param name="autoSubmitted">Whether the final execution submitted without Result Review.</param>
        /// <returns>The configuration, output, tier, validity and Condition counts.</returns>
        internal static JsonObject Execution(ExecutionOutcome outcome, string configuration, bool autoSubmitted)
        {
            ExecutionRecord record = outcome.Record;
            int conditionsTrue = 0;
            int conditionsFalse = 0;
            IReadOnlyList<EventRecord> events = record.Events;
            for (int index = 0; index < events.Count; index++)
            {
                string subtype = events[index].Evidence.Subtype;
                if (subtype == ExecutionEventSubtypes.ConditionTrue)
                    conditionsTrue++;
                else if (subtype == ExecutionEventSubtypes.ConditionFalse)
                    conditionsFalse++;
            }

            return new JsonObjectBuilder()
                .Add("executed", configuration)
                .Add("output", record.FinalState.FinalOutput.Value)
                .Add("tier", outcome.Evaluation.Reached?.ToString())
                .Add("validity", record.ResultValidity.ToString())
                .Add("conditionsTrue", conditionsTrue)
                .Add("conditionsFalse", conditionsFalse)
                .Add("autoSubmitted", autoSubmitted)
                .Build();
        }
    }
}