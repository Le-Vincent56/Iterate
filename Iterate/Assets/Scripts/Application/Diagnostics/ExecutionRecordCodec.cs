using System;
using System.Collections.Generic;
using Iterate.Application.Content.Json;
using Iterate.Application.Persistence;
using Iterate.Domain.Trace;
using Iterate.Domain.Values;

namespace Iterate.Application.Diagnostics
{
    /// <summary>
    /// Reads and writes an <see cref="ExecutionRecord"/>, delegating each sub-graph to its own codec so
    /// a change to one shape edits one small file. Owns the record-level members: the histories, the
    /// safety counts and status, the final state, the defects and the completion status.
    ///
    /// Derived members are never written — the record's constructor recomputes them, so a round trip
    /// proves the inputs survived rather than that the outputs were copied. Nothing is constructed until
    /// the whole document has been read without defect, because several value types in this graph reject
    /// their own default and would throw rather than report.
    ///
    /// In this card the events array is written empty and read as such; the codecs that fill it follow.
    /// </summary>
    public static class ExecutionRecordCodec
    {
        /// <summary>
        /// The format version a generated execution record is written at.
        /// </summary>
        public const int FormatVersion = 1;
        private const string Path = "record";

        /// <summary>
        /// Writes a record as a payload.
        /// </summary>
        /// <param name="record">The record to write.</param>
        /// <returns>The payload.</returns>
        public static JsonObject ToJson(ExecutionRecord record)
        {
            if (record == null)
                throw new ArgumentException("A record codec requires a record.", nameof(record));
            
            List<JsonValue> units = new(record.Units.Count);
            for (int index = 0; index < record.Units.Count; index++)
            {
                units.Add(RuntimeUnitCodec.ToJson(record.Units[index]));
            }
            
            List<JsonValue> events = new(record.Events.Count);
            for (int index = 0; index < record.Events.Count; index++)
            {
                events.Add(EventEvidenceCodec.ToJson(record.Events[index]));
            }

            List<JsonValue> defects = new(record.Defects.Count);
            for (int index = 0; index < record.Defects.Count; index++)
            {
                EvidenceDefect defect = record.Defects[index];
                defects.Add(new JsonObjectBuilder()
                    .Add("event", EventOrNull(defect.Event))
                    .Add("unit", UnitOrNull(defect.Unit))
                    .Add("fieldName", defect.FieldName)
                    .Add("reason", defect.Reason)
                    .Build());
            }

            return new JsonObjectBuilder()
                .Add("header", EvidenceHeaderCodec.ToJson(record.Header))
                .Add("units", JsonValues.Array(units))
                .Add("events", JsonValues.Array(events))
                .Add("traversalOrder", Units(record.TraversalOrder))
                .Add("thresholdHistory", Events(record.ThresholdHistory))
                .Add("counterHistory", Events(record.CounterHistory))
                .Add("safetyCounts", new JsonObjectBuilder()
                    .Add("lineageDepthHighWater", record.SafetyCounts.LineageDepthHighWater)
                    .Add("addedDescendants", record.SafetyCounts.AddedDescendants)
                    .Add("sourceExecutionUnits", record.SafetyCounts.SourceExecutionUnits)
                    .Add("effectReactions", record.SafetyCounts.EffectReactions)
                    .Add("operationTransformations", record.SafetyCounts.OperationTransformations)
                    .Build())
                .Add("safetyStatus", record.SafetyStatus.ToString())
                .Add("finalState", new JsonObjectBuilder()
                    .Add("value", record.FinalState.FinalValue.Value)
                    .Add("signal", record.FinalState.FinalSignal.Value)
                    .Add("score", record.FinalState.FinalScore.Value)
                    .Add("output", record.FinalState.FinalOutput.Value)
                    .Build())
                .Add("defects", JsonValues.Array(defects))
                .Add("completionStatus", record.CompletionStatus.ToString())
                .Build();
        }

        /// <summary>
        /// Reads a record from a payload.
        /// </summary>
        /// <param name="payload">The payload to read.</param>
        /// <returns>The rebuilt record, or a defect naming what was wrong.</returns>
        public static CodecResult<ExecutionRecord> FromJson(JsonObject payload)
        {
            if (payload == null)
                return CodecResult<ExecutionRecord>.Fail($"{Path}: the payload is absent.");

            JsonMemberReader reader = new(payload, Path);
            ExecutionEvidenceHeader header = EvidenceHeaderCodec.FromJson(reader.Child("header"));
            List<RuntimeUnitRecord> units = ReadUnits(reader);
            List<EventRecord> events = ReadEvents(reader);
            List<RuntimeUnitID> traversalOrder = ReadUnitIDs(reader, "traversalOrder");
            List<TraceEventID> thresholdHistory = ReadEventIDs(reader, "thresholdHistory");
            List<TraceEventID> counterHistory = ReadEventIDs(reader, "counterHistory");

            JsonMemberReader counts = reader.Child("safetyCounts");
            int lineageDepthHighWater = counts.Int("lineageDepthHighWater");
            int addedDescendants = counts.Int("addedDescendants");
            int sourceExecutionUnits = counts.Int("sourceExecutionUnits");
            int effectReactions = counts.Int("effectReactions");
            int operationTransformations = counts.Int("operationTransformations");

            SafetyStatus safetyStatus = reader.Enum<SafetyStatus>("safetyStatus");

            JsonMemberReader finalState = reader.Child("finalState");
            int finalValue = finalState.Int("value");
            int finalSignal = finalState.Int("signal");
            int finalScore = finalState.Int("score");
            int finalOutput = finalState.Int("output");

            List<EvidenceDefect> defects = ReadDefects(reader);
            ExecutionCompletionStatus completionStatus = reader.Enum<ExecutionCompletionStatus>("completionStatus");

            if (reader.Failed)
                return CodecResult<ExecutionRecord>.Fail(reader.Defect);

            try
            {
                return CodecResult<ExecutionRecord>.Ok(new ExecutionRecord(
                    header,
                    units,
                    events,
                    traversalOrder,
                    thresholdHistory,
                    counterHistory,
                    new SafetyCounts(
                        lineageDepthHighWater,
                        addedDescendants,
                        sourceExecutionUnits,
                        effectReactions,
                        operationTransformations
                    ),
                    safetyStatus,
                    new FinalExecutionState(
                        new ValueAmount(finalValue),
                        new SignalValue(finalSignal),
                        new ScoreValue(finalScore),
                        new ScoreValue(finalOutput)
                    ),
                    defects,
                    completionStatus
                ));
            }
            catch (ArgumentException exception)
            {
                return CodecResult<ExecutionRecord>.Fail($"{Path}: {exception.Message}");
            }
        }

        /// <summary>
        /// Writes a list of instance identities as integers.
        /// </summary>
        /// <param name="instances">The identities to write.</param>
        /// <returns>The array.</returns>
        internal static JsonArray Instances(IReadOnlyList<InstanceID> instances)
        {
            List<JsonValue> items = new(instances.Count);
            for (int index = 0; index < instances.Count; index++)
            {
                items.Add(JsonValues.Integer(instances[index].Value));
            }

            return JsonValues.Array(items);
        }

        /// <summary>
        /// Writes a list of unit identities as integers.
        /// </summary>
        /// <param name="units">The identities to write.</param>
        /// <returns>The array.</returns>
        internal static JsonArray Units(IReadOnlyList<RuntimeUnitID> units)
        {
            List<JsonValue> items = new(units.Count);
            for (int index = 0; index < units.Count; index++)
            {
                items.Add(JsonValues.Integer(units[index].Value));
            }

            return JsonValues.Array(items);
        }

        /// <summary>
        /// Writes a list of event identities as integers.
        /// </summary>
        /// <param name="events">The identities to write.</param>
        /// <returns>The array.</returns>
        internal static JsonArray Events(IReadOnlyList<TraceEventID> events)
        {
            List<JsonValue> items = new(events.Count);
            for (int index = 0; index < events.Count; index++)
            {
                items.Add(JsonValues.Integer(events[index].Value));
            }

            return JsonValues.Array(items);
        }

        /// <summary>
        /// Writes an optional instance identity.
        /// </summary>
        /// <param name="instance">The identity, or null.</param>
        /// <returns>The integer or the null literal.</returns>
        internal static JsonValue InstanceOrNull(InstanceID? instance)
        {
            return instance.HasValue ? JsonValues.Integer(instance.Value.Value) : JsonValues.Null();
        }

        /// <summary>
        /// Writes an optional unit identity.
        /// </summary>
        /// <param name="unit">The identity, or null.</param>
        /// <returns>The integer or the null literal.</returns>
        internal static JsonValue UnitOrNull(RuntimeUnitID? unit)
        {
            return unit.HasValue ? JsonValues.Integer(unit.Value.Value) : JsonValues.Null();
        }

        /// <summary>
        /// Writes an optional event identity.
        /// </summary>
        /// <param name="traceEvent">The identity, or null.</param>
        /// <returns>The integer or the null literal.</returns>
        internal static JsonValue EventOrNull(TraceEventID? traceEvent)
        {
            return traceEvent.HasValue ? JsonValues.Integer(traceEvent.Value.Value) : JsonValues.Null();
        }

        /// <summary>
        /// Reads a required array of instance identities.
        /// </summary>
        /// <param name="reader">The reader to read from.</param>
        /// <param name="key">The member name.</param>
        /// <returns>The identities, or an empty list when a defect was recorded.</returns>
        internal static List<InstanceID> ReadInstances(JsonMemberReader reader, string key)
        {
            List<InstanceID> instances = new();
            JsonArray items = reader.Array(key);
            if (items == null)
                return instances;

            for (int index = 0; index < items.Items.Count; index++)
            {
                if (items.Items[index] is not JsonNumber { IsInteger: true } number || number.IntegerValue < 1)
                {
                    reader.Fail(key, "an entry is not an instance identity.");
                    return instances;
                }

                instances.Add(new InstanceID((int)number.IntegerValue));
            }

            return instances;
        }

        /// <summary>
        /// Reads a required array of unit identities.
        /// </summary>
        /// <param name="reader">The reader to read from.</param>
        /// <param name="key">The member name.</param>
        /// <returns>The identities, or an empty list when a defect was recorded.</returns>
        internal static List<RuntimeUnitID> ReadUnitIDs(JsonMemberReader reader, string key)
        {
            List<RuntimeUnitID> units = new();
            JsonArray items = reader.Array(key);
            if (items == null)
                return units;

            for (int index = 0; index < items.Items.Count; index++)
            {
                if (items.Items[index] is not JsonNumber number || !number.IsInteger || number.IntegerValue < 1)
                {
                    reader.Fail(key, "an entry is not a unit identity.");
                    return units;
                }

                units.Add(new RuntimeUnitID((int)number.IntegerValue));
            }

            return units;
        }

        /// <summary>
        /// Reads a required array of event identities.
        /// </summary>
        /// <param name="reader">The reader to read from.</param>
        /// <param name="key">The member name.</param>
        /// <returns>The identities, or an empty list when a defect was recorded.</returns>
        internal static List<TraceEventID> ReadEventIDs(JsonMemberReader reader, string key)
        {
            List<TraceEventID> events = new();
            JsonArray items = reader.Array(key);
            if (items == null)
                return events;

            for (int index = 0; index < items.Items.Count; index++)
            {
                if (items.Items[index] is not JsonNumber number || !number.IsInteger || number.IntegerValue < 1)
                {
                    reader.Fail(key, "an entry is not an event identity.");
                    return events;
                }

                events.Add(new TraceEventID((int)number.IntegerValue));
            }

            return events;
        }

        /// <summary>
        /// Reads an optional instance identity written as an integer or a null literal.
        /// </summary>
        /// <param name="reader">The reader to read from.</param>
        /// <param name="key">The member name.</param>
        /// <returns>The identity, or null.</returns>
        internal static InstanceID? ReadInstanceOrNull(JsonMemberReader reader, string key)
        {
            int? value = reader.OptionalInt(key);
            return value.HasValue ? new InstanceID(value.Value) : null;
        }

        /// <summary>
        /// Reads an optional unit identity written as an integer or a null literal.
        /// </summary>
        /// <param name="reader">The reader to read from.</param>
        /// <param name="key">The member name.</param>
        /// <returns>The identity, or null.</returns>
        internal static RuntimeUnitID? ReadUnitOrNull(JsonMemberReader reader, string key)
        {
            int? value = reader.OptionalInt(key);
            return value.HasValue ? new RuntimeUnitID(value.Value) : null;
        }

        /// <summary>
        /// Reads an optional event identity written as an integer or a null literal.
        /// </summary>
        /// <param name="reader">The reader to read from.</param>
        /// <param name="key">The member name.</param>
        /// <returns>The identity, or null.</returns>
        internal static TraceEventID? ReadEventOrNull(JsonMemberReader reader, string key)
        {
            int? value = reader.OptionalInt(key);
            return value.HasValue ? new TraceEventID(value.Value) : null;
        }

        /// <summary>
        /// Opens a reader over one array element, failing the parent when the element is not an object.
        /// </summary>
        /// <param name="parent">The reader the array came from.</param>
        /// <param name="key">The array's member name.</param>
        /// <param name="items">The array.</param>
        /// <param name="index">The element index.</param>
        /// <returns>The element's reader, or null.</returns>
        internal static JsonMemberReader Element(
            JsonMemberReader parent,
            string key,
            JsonArray items,
            int index
        )
        {
            if (items.Items[index] is JsonObject element)
                return new JsonMemberReader(element, key);

            parent.Fail(key, "an entry is not an object.");
            return null;
        }

        /// <summary>
        /// Carries an element reader's defect up to the reader the entry point checks.
        /// </summary>
        /// <param name="parent">The reader the array came from.</param>
        /// <param name="key">The array's member name.</param>
        /// <param name="element">The element's reader.</param>
        /// <returns>True when a defect was carried up.</returns>
        internal static bool Propagate(JsonMemberReader parent, string key, JsonMemberReader element)
        {
            if (!element.Failed)
                return false;

            parent.Fail(key, element.Defect);
            return true;
        }

        /// <summary>
        /// Reads the unit records.
        /// </summary>
        /// <param name="reader">The document reader.</param>
        /// <returns>The units, or an empty list when a defect was recorded.</returns>
        private static List<RuntimeUnitRecord> ReadUnits(JsonMemberReader reader)
        {
            List<RuntimeUnitRecord> units = new();
            JsonArray items = reader.Array("units");
            if (items == null)
                return units;

            for (int index = 0; index < items.Items.Count; index++)
            {
                JsonMemberReader element = Element(reader, "units", items, index);
                if (element == null)
                    return units;

                RuntimeUnitRecord unit = RuntimeUnitCodec.FromJson(element);
                if (Propagate(reader, "units", element))
                    return units;

                units.Add(unit);
            }

            return units;
        }
        
        /// <summary>
        /// Reads the event records.
        /// </summary>
        /// <param name="reader">The document reader.</param>
        /// <returns>The events, or an empty list when a defect was recorded.</returns>
        private static List<EventRecord> ReadEvents(JsonMemberReader reader)
        {
            List<EventRecord> events = new();
            JsonArray items = reader.Array("events");
            if (items == null)
                return events;

            for (int index = 0; index < items.Items.Count; index++)
            {
                JsonMemberReader element = Element(reader, "events", items, index);
                if (element == null)
                    return events;

                EventRecord record = EventEvidenceCodec.FromJson(element);
                if (Propagate(reader, "events", element))
                    return events;

                events.Add(record);
            }

            return events;
        }

        /// <summary>
        /// Reads the evidence defects.
        /// </summary>
        /// <param name="reader">The document reader.</param>
        /// <returns>The defects, or an empty list when a defect was recorded.</returns>
        private static List<EvidenceDefect> ReadDefects(JsonMemberReader reader)
        {
            List<EvidenceDefect> defects = new();
            JsonArray items = reader.Array("defects");
            if (items == null)
                return defects;

            for (int index = 0; index < items.Items.Count; index++)
            {
                JsonMemberReader element = Element(reader, "defects", items, index);
                if (element == null)
                    return defects;

                TraceEventID? anchorEvent = ReadEventOrNull(element, "event");
                RuntimeUnitID? anchorUnit = ReadUnitOrNull(element, "unit");
                string fieldName = element.String("fieldName");
                string reason = element.String("reason");
                if (Propagate(reader, "defects", element))
                    return defects;

                defects.Add(new EvidenceDefect(anchorEvent, anchorUnit, fieldName, reason));
            }

            return defects;
        }
    }
}