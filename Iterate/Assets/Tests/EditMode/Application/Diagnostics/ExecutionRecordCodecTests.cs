using System;
using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Application.Content.Json;
using Iterate.Application.Diagnostics;
using Iterate.Application.Persistence;
using Iterate.Domain.Compilation;
using Iterate.Domain.Trace;
using Iterate.Domain.Values;

namespace Iterate.Application.Diagnostics.Tests
{
    /// <summary>
    /// Tests <see cref="ExecutionRecordCodec"/> over the header, units and record-level members. A
    /// diagnostic artifact is only worth writing if reading it back gives the same record, so every
    /// fixture round-trips to an equal record under the graph's own element-wise equality rather than to
    /// a hand-checked subset of its fields.
    ///
    /// Events are written as an empty array in this card and decoded as such; the three sub-graph codecs
    /// that fill them follow, and the round trip only becomes complete then.
    /// </summary>
    public sealed class ExecutionRecordCodecTests
    {
        [Test]
        public void RoundTrip_OfTheMinimalRecord_IsAnEqualRecord()
        {
            AssertRoundTrips(RecordFixtures.Minimal());
        }

        [Test]
        public void RoundTrip_OfAnAbortedRecord_IsAnEqualRecord()
        {
            AssertRoundTrips(RecordFixtures.Aborted());
        }

        [Test]
        public void RoundTrip_OfARecordWithDefects_IsAnEqualRecord()
        {
            AssertRoundTrips(RecordFixtures.WithDefects());
        }

        [Test]
        public void RoundTrip_OfARecordWithAStructureContext_IsAnEqualRecord()
        {
            AssertRoundTrips(RecordFixtures.WithStructureContext());
        }

        [Test]
        public void RoundTrip_OfARecordWithAnAddedExecution_IsAnEqualRecord()
        {
            AssertRoundTrips(RecordFixtures.WithAddedExecution());
        }

        [Test]
        public void RoundTrip_RecomputesTheDerivedMembersToTheSameValues()
        {
            // TraceCompleteness, ResultValidity and HandoffStatus are derived by the constructor rather
            // than written, so a round trip proves the inputs they derive from survived — not that the
            // derived values were copied across.
            ExecutionRecord source = RecordFixtures.Aborted();

            ExecutionRecord decoded = Decode(source);

            Assert.AreEqual(source.TraceCompleteness, decoded.TraceCompleteness);
            Assert.AreEqual(source.ResultValidity, decoded.ResultValidity);
            Assert.AreEqual(source.HandoffStatus, decoded.HandoffStatus);
        }

        [Test]
        public void RoundTrip_KeepsAStructureContextsNullRepeatIdentityNull()
        {
            // Null and empty are different states here and the constructor rejects the empty one, so a
            // codec that wrote null as "" would make the record unreadable rather than merely wrong.
            ExecutionRecord decoded = Decode(RecordFixtures.WithStructureContext());

            StructureContext context = decoded.Units[0].Opening.StructureContext;
            Assert.IsNotNull(context);
            Assert.IsNull(context.RepeatIterationIdentity);
            Assert.AreEqual("condition-evaluation-1", context.ConditionEvaluationIdentity);
            CollectionAssert.AreEqual(new[] { new InstanceID(4), new InstanceID(5) }, context.StructureAncestry);
        }

        [Test]
        public void RoundTrip_KeepsAnAbsentStructureContextNull()
        {
            ExecutionRecord decoded = Decode(RecordFixtures.Minimal());

            Assert.IsNull(decoded.Units[0].Opening.StructureContext);
        }

        [Test]
        public void RoundTrip_RebuildsALineageByAppending()
        {
            ExecutionRecord decoded = Decode(RecordFixtures.WithAddedExecution());

            EffectOriginLineage lineage = decoded.Units[1].Opening.Lineage;
            Assert.AreEqual(2, lineage.Depth);
            CollectionAssert.AreEqual(new[] { new InstanceID(9), new InstanceID(11) }, lineage.Entries);
        }

        [Test]
        public void RoundTrip_KeepsAnEmptyLineageEmpty()
        {
            ExecutionRecord decoded = Decode(RecordFixtures.Minimal());

            Assert.AreEqual(0, decoded.Units[0].Opening.Lineage.Depth);
        }

        [Test]
        public void RoundTrip_KeepsEveryNullableOpeningMemberOnBothSides()
        {
            ExecutionRecord core = Decode(RecordFixtures.Minimal());
            ExecutionRecord added = Decode(RecordFixtures.WithAddedExecution());

            RuntimeUnitOpening coreOwned = core.Units[0].Opening;
            Assert.IsNull(coreOwned.HostInstance);
            Assert.AreEqual("core-line-1", coreOwned.CoreLineIdentity);
            Assert.IsNull(coreOwned.OriginatingRequestIdentity);
            Assert.IsNull(coreOwned.ParentUnit);
            Assert.AreEqual(new SourcePosition(1), coreOwned.Position);

            RuntimeUnitOpening playerOwned = added.Units[1].Opening;
            Assert.AreEqual(new InstanceID(9), playerOwned.HostInstance);
            Assert.IsNull(playerOwned.CoreLineIdentity);
            Assert.AreEqual("request-1", playerOwned.OriginatingRequestIdentity);
            Assert.AreEqual(new RuntimeUnitID(1), playerOwned.ParentUnit);
        }

        [Test]
        public void RoundTrip_KeepsAClosuresNullableMembers()
        {
            ExecutionRecord withOriginal = Decode(RecordFixtures.Minimal());
            ExecutionRecord withoutOriginal = Decode(RecordFixtures.Aborted());

            Assert.AreEqual(EventDisposition.Skipped, withOriginal.Units[0].Closure.OriginalDisposition);
            Assert.AreEqual(new TraceEventID(1), withOriginal.Units[0].Closure.PrimaryOperationEvent);
            Assert.IsNull(withoutOriginal.Units[0].Closure.OriginalDisposition);
            Assert.IsNull(withoutOriginal.Units[0].Closure.PrimaryOperationEvent);
        }

        [Test]
        public void RoundTrip_KeepsDefectAnchorsDistinct()
        {
            ExecutionRecord decoded = Decode(RecordFixtures.WithDefects());

            Assert.AreEqual(2, decoded.Defects.Count);
            Assert.AreEqual(new RuntimeUnitID(1), decoded.Defects[0].Unit);
            Assert.IsNull(decoded.Defects[0].Event);
            Assert.IsNull(decoded.Defects[1].Unit);
            Assert.IsNull(decoded.Defects[1].Event);
        }

        [Test]
        public void ToJson_WritesEventsAsAnEmptyArrayForNow()
        {
            JsonObject payload = ExecutionRecordCodec.ToJson(RecordFixtures.Minimal());

            Assert.IsTrue(payload.TryGet("events", out JsonValue events));
            Assert.AreEqual(0, ((JsonArray)events).Items.Count);
        }

        [Test]
        public void ToJson_WritesTheSameBytesForTheSameRecord()
        {
            Assert.AreEqual(
                JsonWriter.Write(ExecutionRecordCodec.ToJson(RecordFixtures.WithAddedExecution())),
                JsonWriter.Write(ExecutionRecordCodec.ToJson(RecordFixtures.WithAddedExecution()))
            );
        }

        [Test]
        public void FormatVersion_IsSeparateFromTheSaveFilesVersion()
        {
            // A generated diagnostic artifact and a save file change shape for different reasons and must
            // be able to move independently, so the two constants are deliberately not shared.
            Assert.AreEqual(1, ExecutionRecordCodec.FormatVersion);
            Assert.AreEqual(1, SaveFiles.FormatVersion);
        }

        [Test]
        public void FromJson_CompletedWithAnAbortedSafetyStatus_IsADefectRatherThanAThrow()
        {
            // The record's constructor cross-checks the two and throws. A hand-edited file must surface
            // that as a defect the reader reports, not as an exception escaping the load path.
            JsonObject payload = ExecutionRecordCodec.ToJson(RecordFixtures.Aborted());
            JsonObject mutated = With(payload, "completionStatus", JsonValues.String("Completed"));

            CodecResult<ExecutionRecord> result = ExecutionRecordCodec.FromJson(mutated);

            Assert.IsFalse(result.Succeeded);
            Assert.IsNull(result.Value);
            Assert.IsNotNull(result.Defect);
        }

        [Test]
        public void FromJson_AnUnknownCompletionStatus_IsADefect()
        {
            JsonObject payload = ExecutionRecordCodec.ToJson(RecordFixtures.Minimal());
            JsonObject mutated = With(payload, "completionStatus", JsonValues.String("Abandoned"));

            CodecResult<ExecutionRecord> result = ExecutionRecordCodec.FromJson(mutated);

            Assert.IsFalse(result.Succeeded);
            StringAssert.Contains("completionStatus", result.Defect);
        }

        [Test]
        public void FromJson_AMissingHeader_IsADefect()
        {
            JsonObject payload = ExecutionRecordCodec.ToJson(RecordFixtures.Minimal());

            CodecResult<ExecutionRecord> result = ExecutionRecordCodec.FromJson(Without(payload, "header"));

            Assert.IsFalse(result.Succeeded);
            StringAssert.Contains("header", result.Defect);
        }

        [Test]
        public void FromJson_ANullPayload_IsADefectRatherThanAThrow()
        {
            CodecResult<ExecutionRecord> result = ExecutionRecordCodec.FromJson(null);

            Assert.IsFalse(result.Succeeded);
            Assert.IsNotNull(result.Defect);
        }

        [Test]
        public void FromJson_EveryTopLevelMemberIsRequired()
        {
            JsonObject payload = ExecutionRecordCodec.ToJson(RecordFixtures.WithStructureContext());

            for (int index = 0; index < payload.Keys.Count; index++)
            {
                string key = payload.Keys[index];

                CodecResult<ExecutionRecord> result = ExecutionRecordCodec.FromJson(Without(payload, key));

                Assert.IsFalse(result.Succeeded, "'" + key + "' decoded while absent.");
                StringAssert.Contains(key, result.Defect);
            }
        }

        [Test]
        public void FromJson_EveryHeaderMemberIsRequired()
        {
            JsonObject payload = ExecutionRecordCodec.ToJson(RecordFixtures.Minimal());
            payload.TryGet("header", out JsonValue header);
            JsonObject headerObject = (JsonObject)header;

            for (int index = 0; index < headerObject.Keys.Count; index++)
            {
                string key = headerObject.Keys[index];
                JsonObject mutated = With(payload, "header", Without(headerObject, key));

                CodecResult<ExecutionRecord> result = ExecutionRecordCodec.FromJson(mutated);

                Assert.IsFalse(result.Succeeded, "header.'" + key + "' decoded while absent.");
                StringAssert.Contains(key, result.Defect);
            }
        }

        [Test]
        public void RoundTrip_OfAQuantityChangePayload_IsAnEqualRecord()
        {
            AssertRoundTrips(RecordFixtures.WithQuantityChange());
        }

        [Test]
        public void RoundTrip_OfARandomDecisionPayload_IsAnEqualRecord()
        {
            AssertRoundTrips(RecordFixtures.WithRandomDecision());
        }

        [Test]
        public void RoundTrip_OfASafetyAbortPayload_IsAnEqualRecord()
        {
            // The only place this payload kind is exercised: no shipped fixture breaches a ceiling, so
            // the Infrastructure suite asserts that absence rather than covering it.
            AssertRoundTrips(RecordFixtures.WithSafetyAbort());
        }

        [Test]
        public void RoundTrip_KeepsAQuantityChangesOptionalParts()
        {
            ExecutionRecord decoded = Decode(RecordFixtures.WithQuantityChange());

            QuantityChangePayload payload = (QuantityChangePayload)decoded.Events[0].Evidence.Payload;
            Assert.AreEqual(new TraceEventID(1), payload.ReplacementLink);
            Assert.IsNotNull(payload.AppliedBounds);
            Assert.AreEqual(0, payload.AppliedBounds.Minimum);
            Assert.AreEqual(99, payload.AppliedBounds.Maximum);
            Assert.AreEqual(1, payload.AppliedModifiers.Count);
            Assert.AreEqual("ADDITIVE", payload.AppliedModifiers[0].Stage);
        }

        [Test]
        public void RoundTrip_KeepsAnAbsentBoundsAndReplacementLinkNull()
        {
            // Bounds reject being wholly absent as a value — the constructor demands at least one bound
            // — so "no bounds at all" has to be a null payload member rather than an empty object.
            ExecutionRecord source = RecordFixtures.WithQuantityChange();
            QuantityChangePayload original = (QuantityChangePayload)source.Events[0].Evidence.Payload;
            ExecutionRecord stripped = ReplacePayload(source, new QuantityChangePayload(
                original.QuantityIdentity,
                original.Category,
                original.Operation,
                original.RequestedAmount,
                original.PriorValue,
                null,
                original.AppliedModifiers,
                null,
                original.FinalDelta,
                original.FinalValue
            ));

            ExecutionRecord decoded = Decode(stripped);
            QuantityChangePayload payload = (QuantityChangePayload)decoded.Events[0].Evidence.Payload;

            Assert.IsNull(payload.AppliedBounds);
            Assert.IsNull(payload.ReplacementLink);
            Assert.AreEqual(stripped, decoded);
        }

        [Test]
        public void RoundTrip_KeepsQualifiersEmptyAndNonEmpty()
        {
            ExecutionRecord withQualifiers = Decode(RecordFixtures.WithQuantityChange());
            ExecutionRecord withoutQualifiers = Decode(RecordFixtures.WithRandomDecision());

            CollectionAssert.AreEqual(new[] { "PRIMARY" }, withQualifiers.Events[0].Evidence.Qualifiers);
            Assert.AreEqual(0, withoutQualifiers.Events[0].Evidence.Qualifiers.Count);
        }

        [Test]
        public void RoundTrip_KeepsAGenuineDecisionContextHash()
        {
            ExecutionRecord source = RecordFixtures.WithRandomDecision();
            RandomDecisionPayload original = (RandomDecisionPayload)source.Events[0].Evidence.Payload;

            RandomDecisionPayload decoded = (RandomDecisionPayload)Decode(source).Events[0].Evidence.Payload;

            Assert.AreEqual(original.Record.Request.Context.Hash, decoded.Record.Request.Context.Hash);
            Assert.AreEqual(original.Record.Request.Context.Components, decoded.Record.Request.Context.Components);
        }

        [Test]
        public void FromJson_ATamperedContextHash_IsADefect()
        {
            // The hash is recomputed from the components rather than trusted, so a document whose stored
            // hash no longer matches its own components is a defect naming the path — which is exactly
            // what a tampered or truncated baseline looks like.
            JsonObject payload = ExecutionRecordCodec.ToJson(RecordFixtures.WithRandomDecision());
            JsonObject context = Descend(payload, "events", "evidence", "payload", "record", "request", "context");
            JsonObject tampered = With(context, "hash", JsonValues.String("1"));

            CodecResult<ExecutionRecord> result = ExecutionRecordCodec.FromJson(
                Rebuild(payload, tampered, "events", "evidence", "payload", "record", "request", "context")
            );

            Assert.IsFalse(result.Succeeded);
            StringAssert.Contains("hash", result.Defect);
        }

        [Test]
        public void FromJson_AnUnknownPayloadKind_IsADefect()
        {
            JsonObject payload = ExecutionRecordCodec.ToJson(RecordFixtures.WithQuantityChange());
            JsonObject kind = Descend(payload, "events", "evidence", "payload");
            JsonObject broken = With(kind, "kind", JsonValues.String("Bartering"));

            CodecResult<ExecutionRecord> result = ExecutionRecordCodec.FromJson(
                Rebuild(payload, broken, "events", "evidence", "payload")
            );

            Assert.IsFalse(result.Succeeded);
            StringAssert.Contains("kind", result.Defect);
        }

        [Test]
        public void RoundTrip_KeepsAnEventWithNoPayloadNull()
        {
            ExecutionRecord source = ReplacePayload(RecordFixtures.WithQuantityChange(), null);

            ExecutionRecord decoded = Decode(source);

            Assert.IsNull(decoded.Events[0].Evidence.Payload);
            Assert.AreEqual(source, decoded);
        }

        /// <summary>
        /// Walks into a document along a path, stepping through the first element of any array it meets.
        /// </summary>
        /// <param name="root">The document root.</param>
        /// <param name="path">The member names to follow.</param>
        /// <returns>The object at the end of the path.</returns>
        private static JsonObject Descend(JsonObject root, params string[] path)
        {
            JsonObject current = root;
            for (int index = 0; index < path.Length; index++)
            {
                current.TryGet(path[index], out JsonValue next);
                current = next is JsonArray array ? (JsonObject)array.Items[0] : (JsonObject)next;
            }

            return current;
        }

        /// <summary>
        /// Rebuilds a document with the object at the end of a path replaced, restoring any array it
        /// stepped through around the replacement.
        /// </summary>
        /// <param name="root">The document root.</param>
        /// <param name="replacement">The object to put at the end of the path.</param>
        /// <param name="path">The member names the replacement sits under.</param>
        /// <returns>The rebuilt document.</returns>
        private static JsonObject Rebuild(JsonObject root, JsonObject replacement, params string[] path)
        {
            if (path.Length == 0)
                return replacement;

            root.TryGet(path[0], out JsonValue next);
            string[] rest = new string[path.Length - 1];
            for (int index = 1; index < path.Length; index++)
            {
                rest[index - 1] = path[index];
            }

            if (next is JsonArray array)
            {
                JsonObject inner = Rebuild((JsonObject)array.Items[0], replacement, rest);
                List<JsonValue> items = new(array.Items) { [0] = inner };
                return With(root, path[0], JsonValues.Array(items));
            }

            return With(root, path[0], Rebuild((JsonObject)next, replacement, rest));
        }

        /// <summary>
        /// Rebuilds a record with its single event's payload replaced.
        /// </summary>
        /// <param name="source">The record to derive from.</param>
        /// <param name="payload">The replacement payload, or null.</param>
        /// <returns>The derived record.</returns>
        private static ExecutionRecord ReplacePayload(ExecutionRecord source, EventPayload payload)
        {
            EventEvidence evidence = source.Events[0].Evidence;
            EventEvidence replaced = new(
                evidence.Family,
                evidence.Subtype,
                evidence.Qualifiers,
                evidence.CausalDepth,
                evidence.ContainingUnit,
                evidence.ParentEvent,
                evidence.CausingEvent,
                evidence.HostInstance,
                evidence.CoreLineIdentity,
                evidence.EffectOriginInstance,
                evidence.Ownership,
                evidence.Position,
                evidence.TargetIdentity,
                evidence.Lineage,
                evidence.AddedExecutionRequestIdentity,
                evidence.AddedExecutionDepth,
                evidence.StructureContext,
                evidence.Disposition,
                evidence.DispositionReason,
                evidence.SafetyStatus,
                payload
            );

            return new ExecutionRecord(
                source.Header,
                source.Units,
                new List<EventRecord> { new(source.Events[0].Identity, replaced) },
                source.TraversalOrder,
                source.ThresholdHistory,
                source.CounterHistory,
                source.SafetyCounts,
                source.SafetyStatus,
                source.FinalState,
                source.Defects,
                source.CompletionStatus
            );
        }

        /// <summary>
        /// Asserts a record survives a round trip through the codec as an equal record.
        /// </summary>
        /// <param name="source">The record to round-trip.</param>
        private static void AssertRoundTrips(ExecutionRecord source)
        {
            Assert.AreEqual(source, Decode(source));
        }

        /// <summary>
        /// Encodes a record and decodes it again, failing the test if the decode reports a defect.
        /// </summary>
        /// <param name="source">The record to round-trip.</param>
        /// <returns>The decoded record.</returns>
        private static ExecutionRecord Decode(ExecutionRecord source)
        {
            CodecResult<ExecutionRecord> result = ExecutionRecordCodec.FromJson(ExecutionRecordCodec.ToJson(source));

            Assert.IsTrue(result.Succeeded, result.Defect);
            return result.Value;
        }

        /// <summary>
        /// Copies an object without one member.
        /// </summary>
        /// <param name="source">The object to copy.</param>
        /// <param name="omitted">The member to leave out.</param>
        /// <returns>The copy.</returns>
        private static JsonObject Without(JsonObject source, string omitted)
        {
            JsonObjectBuilder builder = new();
            for (int index = 0; index < source.Keys.Count; index++)
            {
                string key = source.Keys[index];
                if (key == omitted)
                    continue;

                source.TryGet(key, out JsonValue value);
                builder.Add(key, value);
            }

            return builder.Build();
        }

        /// <summary>
        /// Copies an object with one member replaced, keeping member order.
        /// </summary>
        /// <param name="source">The object to copy.</param>
        /// <param name="replaced">The member to replace.</param>
        /// <param name="value">The replacement value.</param>
        /// <returns>The copy.</returns>
        private static JsonObject With(JsonObject source, string replaced, JsonValue value)
        {
            JsonObjectBuilder builder = new();
            for (int index = 0; index < source.Keys.Count; index++)
            {
                string key = source.Keys[index];
                source.TryGet(key, out JsonValue existing);
                builder.Add(key, key == replaced ? value : existing);
            }

            return builder.Build();
        }
    }
}
