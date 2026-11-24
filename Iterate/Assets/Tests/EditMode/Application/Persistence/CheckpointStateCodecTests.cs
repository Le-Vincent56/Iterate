using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Application.Content.Json;
using Iterate.Application.Persistence;
using Iterate.Domain.Progression;
using Iterate.Domain.Values;

namespace Iterate.Application.Persistence.Tests
{
    /// <summary>
    /// Tests <see cref="CheckpointStateCodec"/>: both checkpoint shapes round-trip to equal records, the
    /// written member order is fixed so two equal checkpoints produce identical bytes, and every member
    /// at every level is required — an absent one is a defect naming it, never a default. The absent-is-
    /// a-defect rule is swept over the whole document rather than sampled, because a codec that quietly
    /// defaults one member of forty is indistinguishable from a correct one until a save is restored.
    /// </summary>
    public sealed class CheckpointStateCodecTests
    {
        [Test]
        public void RoundTrip_OfAFullCheckpoint_IsAnEqualRecord()
        {
            CheckpointState source = CheckpointFixtures.Full();

            CodecResult<CheckpointState> result = Decode(source);

            Assert.IsTrue(result.Succeeded, result.Defect);
            AssertEqual(source, result.Value);
        }

        [Test]
        public void RoundTrip_OfATutorialCheckpoint_IsAnEqualRecord()
        {
            CheckpointState source = CheckpointFixtures.Tutorial();

            CodecResult<CheckpointState> result = Decode(source);

            Assert.IsTrue(result.Succeeded, result.Defect);
            AssertEqual(source, result.Value);
            Assert.IsNull(result.Value.Route);
            Assert.IsNull(result.Value.Branch);
        }

        [Test]
        public void ToJson_WritesTheTopLevelMembersInTheFixedOrder()
        {
            JsonObject payload = CheckpointStateCodec.ToJson(CheckpointFixtures.Full());

            CollectionAssert.AreEqual(
                new[]
                {
                    "catalogRevision", "randomServiceRevision", "systemIdentity", "sessionIdentity",
                    "sessionSeedIdentity", "process", "route", "consecutiveFailures",
                    "lastAllocatedInstance", "ordinals", "repository", "branch", "economy"
                },
                payload.Keys
            );
        }

        [Test]
        public void ToJson_WritesTheEconomyMembersInTheFixedOrder()
        {
            JsonObject payload = CheckpointStateCodec.ToJson(CheckpointFixtures.Full());
            payload.TryGet("economy", out JsonValue economy);

            CollectionAssert.AreEqual(
                new[]
                {
                    "tokens", "ramCapacity", "starter", "dependencies", "utilities", "grants",
                    "transactionCounters"
                },
                ((JsonObject)economy).Keys
            );
        }

        [Test]
        public void ToJson_WritesTheSameBytesForTheSameCheckpoint()
        {
            Assert.AreEqual(
                CheckpointFixtures.Json(CheckpointFixtures.Full()),
                CheckpointFixtures.Json(CheckpointFixtures.Full())
            );
        }

        [Test]
        public void ToJson_StampsTheFormatVersionFirst()
        {
            StringAssert.StartsWith("{\n  \"formatVersion\": 1,\n", CheckpointFixtures.Json(CheckpointFixtures.Full()));
        }

        [Test]
        public void ToJson_WritesAnAbsentRouteAndBranchAsNull()
        {
            JsonObject payload = CheckpointStateCodec.ToJson(CheckpointFixtures.Tutorial());

            Assert.IsTrue(payload.TryGet("route", out JsonValue route));
            Assert.IsInstanceOf<JsonNull>(route);
            Assert.IsTrue(payload.TryGet("branch", out JsonValue branch));
            Assert.IsInstanceOf<JsonNull>(branch);
        }

        [Test]
        public void ToJson_WritesIdentitiesAsIntegersAndEnumsByName()
        {
            JsonObject payload = CheckpointStateCodec.ToJson(CheckpointFixtures.Full());
            payload.TryGet("repository", out JsonValue repository);
            JsonObject item = (JsonObject)((JsonArray)repository).Items[0];

            Assert.IsTrue(item.TryGet("instance", out JsonValue instance));
            Assert.AreEqual(1L, ((JsonNumber)instance).IntegerValue);
            Assert.IsTrue(item.TryGet("origin", out JsonValue origin));
            Assert.AreEqual("Starter", ((JsonString)origin).Value);
        }

        [Test]
        public void RecordFailure_IncrementsWithoutMutatingTheSource()
        {
            CheckpointState source = CheckpointFixtures.Full();

            CheckpointState recorded = source.RecordFailure();

            Assert.AreEqual(3, recorded.ConsecutiveFailures);
            Assert.AreEqual(2, source.ConsecutiveFailures, "a derivation leaves the value it came from alone.");
        }

        [Test]
        public void RecordFailure_SurvivesARoundTrip()
        {
            CheckpointState source = CheckpointFixtures.Full().RecordFailure().RecordFailure();

            CodecResult<CheckpointState> result = Decode(source);

            Assert.IsTrue(result.Succeeded, result.Defect);
            Assert.AreEqual(4, result.Value.ConsecutiveFailures);
        }

        [Test]
        public void FromJson_EveryMemberAtEveryLevelIsRequired()
        {
            // Swept rather than sampled: each member of the whole document is removed in turn and the
            // decode must fail naming it. A nullable member is no exception — `route: null` is a value,
            // an absent `route` is missing data, and the two must stay distinguishable.
            JsonObject payload = CheckpointStateCodec.ToJson(CheckpointFixtures.Full());
            List<Removal> removals = Removals(payload);

            Assert.Greater(removals.Count, 30, "the sweep must actually reach the nested levels.");
            foreach (Removal removal in removals)
            {
                CodecResult<CheckpointState> result = CheckpointStateCodec.FromJson(removal.Root);

                Assert.IsFalse(result.Succeeded, $"'{removal.Member}' decoded while absent.");
                Assert.IsNull(result.Value, $"'{removal.Member}' yielded a state despite a defect.");
                StringAssert.Contains(removal.Member, result.Defect);
            }
        }

        [Test]
        public void FromJson_AWrongCaseEnumName_IsADefect()
        {
            JsonObject payload = CheckpointStateCodec.ToJson(CheckpointFixtures.Full());
            payload.TryGet("repository", out JsonValue repository);
            JsonArray items = (JsonArray)repository;
            JsonObject broken = With((JsonObject)items.Items[0], "origin", JsonValues.String("starter"));
            JsonObject mutated = With(payload, "repository", ReplaceItem(items, 0, broken));

            CodecResult<CheckpointState> result = CheckpointStateCodec.FromJson(mutated);

            Assert.IsFalse(result.Succeeded);
            StringAssert.Contains("origin", result.Defect);
        }

        [Test]
        public void FromJson_AnUnknownEnumName_IsADefect()
        {
            JsonObject payload = CheckpointStateCodec.ToJson(CheckpointFixtures.Full());
            payload.TryGet("economy", out JsonValue economy);
            JsonObject broken = With((JsonObject)economy, "transactionCounters", JsonValues.Array(
                new List<JsonValue>
                {
                    new JsonObjectBuilder().Add("kind", "Bartering").Add("count", 1L).Build()
                }
            ));

            CodecResult<CheckpointState> result = CheckpointStateCodec.FromJson(With(payload, "economy", broken));

            Assert.IsFalse(result.Succeeded);
            StringAssert.Contains("kind", result.Defect);
        }

        [Test]
        public void FromJson_ANullPayload_IsADefectRatherThanAThrow()
        {
            CodecResult<CheckpointState> result = CheckpointStateCodec.FromJson(null);

            Assert.IsFalse(result.Succeeded);
            Assert.IsNotNull(result.Defect);
        }

        [Test]
        public void FromJson_AnEmptyBranchArray_IsAConfirmedBranchHoldingNothing()
        {
            // An empty Branch and an absent Branch are different states: the player confirmed a Branch
            // with nothing in it, or never reached the Branch screen at all.
            JsonObject payload = CheckpointStateCodec.ToJson(CheckpointFixtures.Full());
            JsonObject mutated = With(payload, "branch", JsonValues.Array(new List<JsonValue>()));

            CodecResult<CheckpointState> result = CheckpointStateCodec.FromJson(mutated);

            Assert.IsTrue(result.Succeeded, result.Defect);
            Assert.IsNotNull(result.Value.Branch);
            Assert.AreEqual(0, result.Value.Branch.Count);
        }

        /// <summary>
        /// Writes a checkpoint through the envelope and reads it back, as a save round trip does.
        /// </summary>
        /// <param name="state">The checkpoint to encode.</param>
        /// <returns>The decode result.</returns>
        private static CodecResult<CheckpointState> Decode(CheckpointState state)
        {
            VersionedDocumentReadResult read = VersionedDocument.Read(
                CheckpointFixtures.Json(state),
                SaveFiles.FormatVersion
            );

            Assert.AreEqual(VersionedDocumentOutcome.Loaded, read.Outcome, read.Reason);
            return CheckpointStateCodec.FromJson(read.Payload);
        }

        /// <summary>
        /// Asserts two checkpoints hold the same values, comparing every list element-wise.
        /// </summary>
        /// <param name="expected">The checkpoint written.</param>
        /// <param name="actual">The checkpoint read back.</param>
        private static void AssertEqual(CheckpointState expected, CheckpointState actual)
        {
            Assert.AreEqual(expected.CatalogRevision, actual.CatalogRevision);
            Assert.AreEqual(expected.RandomServiceRevision, actual.RandomServiceRevision);
            Assert.AreEqual(expected.SystemIdentity, actual.SystemIdentity);
            Assert.AreEqual(expected.SessionIdentity, actual.SessionIdentity);
            Assert.AreEqual(expected.SessionSeedIdentity, actual.SessionSeedIdentity);
            Assert.AreEqual(expected.Process, actual.Process);
            Assert.AreEqual(expected.Route, actual.Route);
            Assert.AreEqual(expected.ConsecutiveFailures, actual.ConsecutiveFailures);
            Assert.AreEqual(expected.LastAllocatedInstance, actual.LastAllocatedInstance);
            CollectionAssert.AreEqual(expected.Ordinals, actual.Ordinals);
            Assert.AreEqual(expected.Repository.Count, actual.Repository.Count);
            for (int index = 0; index < expected.Repository.Count; index++)
            {
                CheckpointRepositoryItem left = expected.Repository[index];
                CheckpointRepositoryItem right = actual.Repository[index];
                Assert.AreEqual(left.Instance, right.Instance);
                Assert.AreEqual(left.Definition, right.Definition);
                Assert.AreEqual(left.Origin, right.Origin);
                Assert.AreEqual(left.StarterProtected, right.StarterProtected);
                Assert.AreEqual(left.Suffix, right.Suffix);
                CollectionAssert.AreEqual(left.Attachments, right.Attachments);
            }

            CollectionAssert.AreEqual(expected.Branch, actual.Branch);
            Assert.AreEqual(expected.Economy.Tokens, actual.Economy.Tokens);
            Assert.AreEqual(expected.Economy.RAMCapacity, actual.Economy.RAMCapacity);
            Assert.AreEqual(expected.Economy.Starter, actual.Economy.Starter);
            CollectionAssert.AreEqual(expected.Economy.Dependencies, actual.Economy.Dependencies);
            CollectionAssert.AreEqual(expected.Economy.Utilities, actual.Economy.Utilities);
            CollectionAssert.AreEqual(expected.Economy.Grants, actual.Economy.Grants);
            CollectionAssert.AreEqual(expected.Economy.TransactionCounters, actual.Economy.TransactionCounters);
        }

        /// <summary>
        /// Every member of a document tree paired with a copy of the root that omits it.
        /// </summary>
        /// <param name="root">The document root.</param>
        /// <returns>One removal per member, at every nesting level.</returns>
        private static List<Removal> Removals(JsonObject root)
        {
            List<Removal> removals = new();
            for (int index = 0; index < root.Keys.Count; index++)
            {
                string key = root.Keys[index];
                removals.Add(new Removal(key, Without(root, key)));
                root.TryGet(key, out JsonValue child);

                if (child is JsonObject nested)
                {
                    foreach (Removal inner in Removals(nested))
                    {
                        removals.Add(new Removal(inner.Member, With(root, key, inner.Root)));
                    }
                }

                if (child is not JsonArray items)
                    continue;

                for (int item = 0; item < items.Items.Count; item++)
                {
                    if (items.Items[item] is not JsonObject element)
                        continue;

                    foreach (Removal inner in Removals(element))
                    {
                        removals.Add(new Removal(inner.Member, With(root, key, ReplaceItem(items, item, inner.Root))));
                    }
                }
            }

            return removals;
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
        /// Copies an object with one member replaced, keeping the member order.
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

        /// <summary>
        /// Copies an array with one item replaced.
        /// </summary>
        /// <param name="source">The array to copy.</param>
        /// <param name="index">The item index to replace.</param>
        /// <param name="value">The replacement item.</param>
        /// <returns>The copy.</returns>
        private static JsonArray ReplaceItem(JsonArray source, int index, JsonValue value)
        {
            List<JsonValue> items = new(source.Items);
            items[index] = value;
            return JsonValues.Array(items);
        }

        /// <summary>
        /// One member of a document paired with a root that omits it.
        /// </summary>
        /// <param name="Member">The member left out.</param>
        /// <param name="Root">The document root without that member.</param>
        private sealed record Removal(string Member, JsonObject Root);
    }
}
