using System.Collections.Generic;
using System.Globalization;
using Iterate.Application.Content.Json;
using Iterate.Domain.Determinism;

namespace Iterate.Application.Diagnostics
{
    /// <summary>
    /// Reads and writes a <see cref="RandomDecisionRecord"/> — the evidence that makes a random decision
    /// reproducible rather than merely recorded.
    ///
    /// Two shapes here are deliberate. The context hash is written as a decimal **string**, because it
    /// is a <c>ulong</c> and a JSON number carries a <c>long</c>; and it is never fed back in, because
    /// <see cref="DecisionContext"/> has a private constructor and only derives. The stored hash is
    /// therefore a cross-check: the context is rebuilt from its components and the two hashes compared,
    /// so a document whose hash no longer matches its own components is a defect naming the path rather
    /// than a decision that silently claims a provenance it does not have.
    /// </summary>
    internal static class RandomDecisionRecordCodec
    {
        /// <summary>
        /// Writes a decision record as an object.
        /// </summary>
        /// <param name="record">The record to write.</param>
        /// <returns>The object.</returns>
        internal static JsonObject ToJson(RandomDecisionRecord record)
        {
            List<JsonValue> candidates = new(record.CandidateEvidence.Count);
            for (int index = 0; index < record.CandidateEvidence.Count; index++)
            {
                CandidateEntry candidate = record.CandidateEvidence[index];
                candidates.Add(new JsonObjectBuilder()
                    .Add("identity", candidate.Identity)
                    .Add("orderingKey", new JsonObjectBuilder()
                        .Add("definitionIdentity", candidate.OrderingKey.DefinitionIdentity)
                        .Add("instanceIdentity", candidate.OrderingKey.InstanceIdentity)
                        .Add("sourceIdentity", candidate.OrderingKey.SourceIdentity)
                        .Add("additionalTieBreak", candidate.OrderingKey.AdditionalTieBreak)
                        .Build())
                    .Add("finalWeight", candidate.FinalWeight.HasValue
                        ? JsonValues.Integer(candidate.FinalWeight.Value)
                        : JsonValues.Null())
                    .Build());
            }

            List<JsonValue> draws = new(record.Draws.Count);
            for (int index = 0; index < record.Draws.Count; index++)
            {
                DrawRecord draw = record.Draws[index];
                draws.Add(new JsonObjectBuilder()
                    .Add("drawOrdinal", draw.DrawOrdinal)
                    .Add("populationBefore", Strings(draw.PopulationBefore))
                    .Add("selectedIdentity", draw.SelectedIdentity)
                    .Add("consequence", draw.Consequence.ToString())
                    .Add("populationAfter", Strings(draw.PopulationAfter))
                    .Build());
            }

            return new JsonObjectBuilder()
                .Add("request", RequestToJson(record.Request))
                .Add("candidateEvidence", JsonValues.Array(candidates))
                .Add("revisionIdentity", record.RevisionIdentity)
                .Add("draws", JsonValues.Array(draws))
                .Add("resultPermutation", Strings(record.ResultPermutation))
                .Add("outcome", new JsonObjectBuilder()
                    .Add("disposition", record.Outcome.Disposition.ToString())
                    .Add("selectedIdentities", Strings(record.Outcome.SelectedIdentities))
                    .Build())
                .Build();
        }

        /// <summary>
        /// Reads a decision record from its reader, recording any defect on it rather than throwing.
        /// </summary>
        /// <param name="reader">The record's reader.</param>
        /// <returns>The record, or null when a defect was recorded.</returns>
        internal static RandomDecisionRecord FromJson(JsonMemberReader reader)
        {
            DecisionRequest request = RequestFromJson(reader.Child("request"));
            List<CandidateEntry> candidates = ReadCandidates(reader);
            string revisionIdentity = reader.String("revisionIdentity");
            List<DrawRecord> draws = ReadDraws(reader);
            List<string> permutation = ReadStrings(reader, "resultPermutation");

            JsonMemberReader outcome = reader.Child("outcome");
            DecisionDisposition disposition = outcome.Enum<DecisionDisposition>("disposition");
            List<string> selected = ReadStrings(outcome, "selectedIdentities");

            if (reader.Failed)
                return null;

            return new RandomDecisionRecord(
                request,
                candidates,
                revisionIdentity,
                draws,
                permutation,
                new DecisionOutcome(disposition, selected)
            );
        }

        /// <summary>
        /// Writes a decision request and its context.
        /// </summary>
        /// <param name="request">The request to write.</param>
        /// <returns>The object.</returns>
        private static JsonObject RequestToJson(DecisionRequest request)
        {
            DecisionContextComponents components = request.Context.Components;
            JsonObject context = new JsonObjectBuilder()
                .Add("components", new JsonObjectBuilder()
                    .Add("sessionSeedIdentity", components.SessionSeedIdentity)
                    .Add("contentRevision", components.ContentRevision)
                    .Add("rulesetRevision", components.RulesetRevision)
                    .Add("systemIdentity", components.SystemIdentity)
                    .Add("processIdentity", components.ProcessIdentity)
                    .Add("scopeIdentity", components.ScopeIdentity)
                    .Add("causingEventIdentity", components.CausingEventIdentity)
                    .Add("effectOriginIdentity", components.EffectOriginIdentity)
                    .Add("selectionPurpose", components.SelectionPurpose)
                    .Add("occurrenceOrdinal", components.OccurrenceOrdinal)
                    .Build())
                .Add("hash", request.Context.Hash.ToString(CultureInfo.InvariantCulture))
                .Build();

            return new JsonObjectBuilder()
                .Add("selectionIdentity", request.SelectionIdentity)
                .Add("selectionPurpose", request.SelectionPurpose)
                .Add("selectionBoundary", request.SelectionBoundary)
                .Add("candidateSource", request.CandidateSource)
                .Add("eligibilityRule", request.EligibilityRule)
                .Add("candidateSnapshotTiming", request.CandidateSnapshotTiming)
                .Add("candidateOrderingRule", request.CandidateOrderingRule)
                .Add("selectionCount", request.SelectionCount)
                .Add("method", request.Method.ToString())
                .Add("replacement", request.Replacement.ToString())
                .Add("weightingRule", request.WeightingRule)
                .Add("tieBreakRule", request.TieBreakRule)
                .Add("unconsumedSuffixDisposition", request.UnconsumedSuffixDisposition)
                .Add("insufficientCandidates", request.InsufficientCandidates.ToString())
                .Add("context", context)
                .Build();
        }

        /// <summary>
        /// Reads a decision request, rebuilding its context by derivation and cross-checking the stored
        /// hash against the one the derivation produces.
        /// </summary>
        /// <param name="reader">The request's reader.</param>
        /// <returns>The request, or null when a defect was recorded.</returns>
        private static DecisionRequest RequestFromJson(JsonMemberReader reader)
        {
            string selectionIdentity = reader.String("selectionIdentity");
            string selectionPurpose = reader.String("selectionPurpose");
            string selectionBoundary = reader.String("selectionBoundary");
            string candidateSource = reader.String("candidateSource");
            string eligibilityRule = reader.String("eligibilityRule");
            string candidateSnapshotTiming = reader.String("candidateSnapshotTiming");
            string candidateOrderingRule = reader.String("candidateOrderingRule");
            int selectionCount = reader.Int("selectionCount");
            SelectionMethod method = reader.Enum<SelectionMethod>("method");
            ReplacementBehavior replacement = reader.Enum<ReplacementBehavior>("replacement");
            string weightingRule = reader.OptionalString("weightingRule");
            string tieBreakRule = reader.String("tieBreakRule");
            string unconsumedSuffixDisposition = reader.OptionalString("unconsumedSuffixDisposition");
            InsufficientCandidateBehavior insufficient =
                reader.Enum<InsufficientCandidateBehavior>("insufficientCandidates");

            JsonMemberReader context = reader.Child("context");
            JsonMemberReader components = context.Child("components");
            string sessionSeedIdentity = components.String("sessionSeedIdentity");
            string contentRevision = components.String("contentRevision");
            string rulesetRevision = components.String("rulesetRevision");
            string systemIdentity = components.String("systemIdentity");
            string processIdentity = components.String("processIdentity");
            string scopeIdentity = components.String("scopeIdentity");
            string causingEventIdentity = components.OptionalString("causingEventIdentity");
            string effectOriginIdentity = components.OptionalString("effectOriginIdentity");
            string componentPurpose = components.String("selectionPurpose");
            int occurrenceOrdinal = components.Int("occurrenceOrdinal");
            string storedHash = context.String("hash");

            if (reader.Failed)
                return null;

            DecisionContext derived = DecisionContext.Derive(new DecisionContextComponents(
                sessionSeedIdentity,
                contentRevision,
                rulesetRevision,
                systemIdentity,
                processIdentity,
                scopeIdentity,
                causingEventIdentity,
                effectOriginIdentity,
                componentPurpose,
                occurrenceOrdinal
            ));

            if (!ulong.TryParse(storedHash, NumberStyles.None, CultureInfo.InvariantCulture, out ulong parsed)
                || parsed != derived.Hash)
            {
                context.Fail("hash", "recomputed hash differs.");
                return null;
            }

            return new DecisionRequest(
                selectionIdentity,
                selectionPurpose,
                selectionBoundary,
                candidateSource,
                eligibilityRule,
                candidateSnapshotTiming,
                candidateOrderingRule,
                selectionCount,
                method,
                replacement,
                weightingRule,
                tieBreakRule,
                unconsumedSuffixDisposition,
                insufficient,
                derived
            );
        }

        /// <summary>
        /// Reads the candidate evidence.
        /// </summary>
        /// <param name="reader">The record's reader.</param>
        /// <returns>The candidates, or an empty list when a defect was recorded.</returns>
        private static List<CandidateEntry> ReadCandidates(JsonMemberReader reader)
        {
            List<CandidateEntry> candidates = new();
            JsonArray items = reader.Array("candidateEvidence");
            if (items == null)
                return candidates;

            for (int index = 0; index < items.Items.Count; index++)
            {
                JsonMemberReader element = ExecutionRecordCodec.Element(reader, "candidateEvidence", items, index);
                if (element == null)
                    return candidates;

                string identity = element.String("identity");
                JsonMemberReader key = element.Child("orderingKey");
                string definitionIdentity = key.String("definitionIdentity");
                string instanceIdentity = key.OptionalString("instanceIdentity");
                string sourceIdentity = key.OptionalString("sourceIdentity");
                string additionalTieBreak = key.OptionalString("additionalTieBreak");
                int? finalWeight = element.OptionalInt("finalWeight");
                if (ExecutionRecordCodec.Propagate(reader, "candidateEvidence", element))
                    return candidates;

                candidates.Add(new CandidateEntry(
                    identity,
                    new CandidateOrderingKey(definitionIdentity, instanceIdentity, sourceIdentity, additionalTieBreak),
                    finalWeight
                ));
            }

            return candidates;
        }

        /// <summary>
        /// Reads the draw records.
        /// </summary>
        /// <param name="reader">The record's reader.</param>
        /// <returns>The draws, or an empty list when a defect was recorded.</returns>
        private static List<DrawRecord> ReadDraws(JsonMemberReader reader)
        {
            List<DrawRecord> draws = new();
            JsonArray items = reader.Array("draws");
            if (items == null)
                return draws;

            for (int index = 0; index < items.Items.Count; index++)
            {
                JsonMemberReader element = ExecutionRecordCodec.Element(reader, "draws", items, index);
                if (element == null)
                    return draws;

                int drawOrdinal = element.Int("drawOrdinal");
                List<string> before = ReadStrings(element, "populationBefore");
                string selectedIdentity = element.String("selectedIdentity");
                ReplacementBehavior consequence = element.Enum<ReplacementBehavior>("consequence");
                List<string> after = ReadStrings(element, "populationAfter");
                if (ExecutionRecordCodec.Propagate(reader, "draws", element))
                    return draws;

                draws.Add(new DrawRecord(drawOrdinal, before, selectedIdentity, consequence, after));
            }

            return draws;
        }

        /// <summary>
        /// Writes a list of strings.
        /// </summary>
        /// <param name="values">The strings to write.</param>
        /// <returns>The array.</returns>
        private static JsonArray Strings(IReadOnlyList<string> values)
        {
            List<JsonValue> items = new(values.Count);
            for (int index = 0; index < values.Count; index++)
            {
                items.Add(JsonValues.String(values[index]));
            }

            return JsonValues.Array(items);
        }

        /// <summary>
        /// Reads a required array of strings.
        /// </summary>
        /// <param name="reader">The reader to read from.</param>
        /// <param name="key">The member name.</param>
        /// <returns>The strings, or an empty list when a defect was recorded.</returns>
        private static List<string> ReadStrings(JsonMemberReader reader, string key)
        {
            List<string> values = new();
            JsonArray items = reader.Array(key);
            if (items == null)
                return values;

            for (int index = 0; index < items.Items.Count; index++)
            {
                if (items.Items[index] is not JsonString text)
                {
                    reader.Fail(key, "an entry is not a string.");
                    return values;
                }

                values.Add(text.Value);
            }

            return values;
        }
    }
}