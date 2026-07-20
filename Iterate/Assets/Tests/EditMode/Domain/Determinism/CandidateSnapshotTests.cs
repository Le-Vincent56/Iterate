using System;
using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Domain.Determinism;

namespace Iterate.Domain.Determinism.Tests
{
    /// <summary>
    /// Tests <see cref="CandidateSnapshot.Create"/> — canonical sorting into the single CAB-EVT-799 order
    /// (so permutation-invariance, CAB-EVT-812, is inherited by every consumer), identity uniqueness
    /// (CAB-EVT-798), ordering-collision rejection (CAB-EVT-806), the all-or-none weight rule, and the
    /// empty-snapshot case — plus <see cref="CandidateEntry"/> construction validation (non-empty identity,
    /// non-null key, non-negative weight, CAB-EVT-818).
    /// </summary>
    public sealed class CandidateSnapshotTests
    {
        [Test]
        public void Create_ShuffledInputOrders_ProduceIdenticalCanonicalOrder()
        {
            // Arrange — four entries whose canonical order by definition is a, b, c, d.
            CandidateEntry a = Unweighted("a", "A");
            CandidateEntry b = Unweighted("b", "B");
            CandidateEntry c = Unweighted("c", "C");
            CandidateEntry d = Unweighted("d", "D");
            CandidateEntry[][] permutations =
            {
                new[] { a, b, c, d },
                new[] { d, c, b, a },
                new[] { b, d, a, c },
                new[] { c, a, d, b },
                new[] { a, c, b, d },
                new[] { d, a, c, b }
            };

            // Act & Assert
            string[] expected = { "a", "b", "c", "d" };
            for (int index = 0; index < permutations.Length; index++)
            {
                CandidateSnapshot snapshot = CandidateSnapshot.Create(permutations[index]);
                Assert.That(Identities(snapshot), Is.EqualTo(expected), $"permutation {index}");
            }
        }

        [Test]
        public void Create_SortsByFullCanonicalKey()
        {
            // Arrange — same definition, differing instance then source then tie-break.
            CandidateEntry entry1 = new("e1", new CandidateOrderingKey("A", "a", "s1", "t1"), null);
            CandidateEntry entry2 = new("e2", new CandidateOrderingKey("A", "a", "s1", "t2"), null);
            CandidateEntry entry3 = new("e3", new CandidateOrderingKey("A", "a", "s2", null), null);
            CandidateEntry entry4 = new("e4", new CandidateOrderingKey("A", "b", null, null), null);

            // Act
            CandidateSnapshot snapshot = CandidateSnapshot.Create(new[] { entry4, entry3, entry2, entry1 });

            // Assert
            Assert.That(Identities(snapshot), Is.EqualTo(new[] { "e1", "e2", "e3", "e4" }));
        }

        [Test]
        public void Create_DuplicateIdentity_Throws()
        {
            CandidateEntry first = Unweighted("dup", "A");
            CandidateEntry second = Unweighted("dup", "B");

            Assert.Throws<ArgumentException>(() => CandidateSnapshot.Create(new[] { first, second }));
        }

        [Test]
        public void Create_OrderingCollision_Throws()
        {
            // Distinct identities but identical ordering keys — a data-integrity defect (CAB-EVT-806).
            CandidateEntry first = new("x", new CandidateOrderingKey("A", null, null, null), null);
            CandidateEntry second = new("y", new CandidateOrderingKey("A", null, null, null), null);

            Assert.Throws<ArgumentException>(() => CandidateSnapshot.Create(new[] { first, second }));
        }

        [Test]
        public void Create_MixedWeightPresence_Throws()
        {
            CandidateEntry weighted = new("w", new CandidateOrderingKey("A", null, null, null), 5);
            CandidateEntry unweighted = new("u", new CandidateOrderingKey("B", null, null, null), null);

            Assert.Throws<ArgumentException>(() => CandidateSnapshot.Create(new[] { weighted, unweighted }));
        }

        [Test]
        public void Create_NullEntryList_Throws()
        {
            Assert.Throws<ArgumentException>(() => CandidateSnapshot.Create(null));
        }

        [Test]
        public void Create_EmptySnapshot_IsLegalAndUnweighted()
        {
            CandidateSnapshot snapshot = CandidateSnapshot.Create(new CandidateEntry[0]);

            Assert.That(snapshot.Count, Is.EqualTo(0));
            Assert.That(snapshot.IsWeighted, Is.False);
        }

        [Test]
        public void Create_AllEntriesWeighted_IsWeightedTrue()
        {
            CandidateEntry first = new("a", new CandidateOrderingKey("A", null, null, null), 3);
            CandidateEntry second = new("b", new CandidateOrderingKey("B", null, null, null), 0);

            CandidateSnapshot snapshot = CandidateSnapshot.Create(new[] { first, second });

            Assert.That(snapshot.IsWeighted, Is.True);
            Assert.That(snapshot.Count, Is.EqualTo(2));
        }

        [Test]
        public void Create_NoEntriesWeighted_IsWeightedFalse()
        {
            CandidateSnapshot snapshot = CandidateSnapshot.Create(new[] { Unweighted("a", "A"), Unweighted("b", "B") });

            Assert.That(snapshot.IsWeighted, Is.False);
        }

        [Test]
        public void CandidateEntry_ZeroWeight_IsAllowed()
        {
            Assert.DoesNotThrow(() => new CandidateEntry("a", new CandidateOrderingKey("A", null, null, null), 0));
        }

        [Test]
        public void CandidateEntry_NegativeWeight_Throws()
        {
            Assert.Throws<ArgumentException>(() => new CandidateEntry(
                "a", new CandidateOrderingKey("A", null, null, null), -1));
        }

        [Test]
        public void CandidateEntry_EmptyIdentity_Throws()
        {
            Assert.Throws<ArgumentException>(() => new CandidateEntry(
                "", new CandidateOrderingKey("A", null, null, null), null));
        }

        [Test]
        public void CandidateEntry_NullOrderingKey_Throws()
        {
            Assert.Throws<ArgumentException>(() => new CandidateEntry("a", null, null));
        }

        private static CandidateEntry Unweighted(string identity, string definitionIdentity)
        {
            return new CandidateEntry(identity, new CandidateOrderingKey(definitionIdentity, null, null, null), null);
        }

        private static string[] Identities(CandidateSnapshot snapshot)
        {
            string[] identities = new string[snapshot.Count];
            for (int index = 0; index < snapshot.Count; index++)
                identities[index] = snapshot.Candidates[index].Identity;

            return identities;
        }
    }
}
