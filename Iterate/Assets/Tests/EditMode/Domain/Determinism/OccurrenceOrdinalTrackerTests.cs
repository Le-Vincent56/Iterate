using System;
using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Domain.Determinism;

namespace Iterate.Domain.Determinism.Tests
{
    /// <summary>
    /// Tests the caller-owned <see cref="OccurrenceOrdinalTracker"/> (CAB-EVT-836/837/838): one-based
    /// ordinals per (purpose, scope) pair, pair independence, empty-argument rejection, and the lifetime
    /// contract — fresh trackers per scope reproduce identical ordinal sequences, while a tracker reused
    /// across scopes diverges from that baseline.
    /// The snapshot and restore seam is covered too: a snapshot is ordered independently of call order,
    /// and a restored tracker continues every key it was given and starts an unseen key at one.
    /// </summary>
    public sealed class OccurrenceOrdinalTrackerTests
    {
        [Test]
        public void Next_FirstCallForPair_ReturnsOne()
        {
            OccurrenceOrdinalTracker tracker = new();

            Assert.That(tracker.Next("Random ordering", "scope-1"), Is.EqualTo(1));
        }

        [Test]
        public void Next_RepeatedCallsForSamePair_Increment()
        {
            OccurrenceOrdinalTracker tracker = new();

            Assert.That(tracker.Next("Random ordering", "scope-1"), Is.EqualTo(1));
            Assert.That(tracker.Next("Random ordering", "scope-1"), Is.EqualTo(2));
            Assert.That(tracker.Next("Random ordering", "scope-1"), Is.EqualTo(3));
        }

        [Test]
        public void Next_DistinctPairs_AreIndependent()
        {
            OccurrenceOrdinalTracker tracker = new();

            Assert.That(tracker.Next("Random ordering", "scope-1"), Is.EqualTo(1));
            Assert.That(tracker.Next("Random ordering", "scope-1"), Is.EqualTo(2));
            // Different purpose, same scope — independent sequence.
            Assert.That(tracker.Next("Reward generation", "scope-1"), Is.EqualTo(1));
            // Same purpose, different scope — independent sequence.
            Assert.That(tracker.Next("Random ordering", "scope-2"), Is.EqualTo(1));
        }

        [Test]
        public void Next_IsCaseSensitivePerPair()
        {
            OccurrenceOrdinalTracker tracker = new();

            Assert.That(tracker.Next("Random ordering", "scope-1"), Is.EqualTo(1));
            // Ordinal string semantics: a differently-cased scope is a distinct pair.
            Assert.That(tracker.Next("Random ordering", "Scope-1"), Is.EqualTo(1));
        }

        [Test]
        public void Next_EmptyOrNullPurpose_Throws([Values("", null)] string purpose)
        {
            OccurrenceOrdinalTracker tracker = new();

            Assert.Throws<ArgumentException>(() => tracker.Next(purpose, "scope-1"));
        }

        [Test]
        public void Next_EmptyOrNullScope_Throws([Values("", null)] string scope)
        {
            OccurrenceOrdinalTracker tracker = new();

            Assert.Throws<ArgumentException>(() => tracker.Next("Random ordering", scope));
        }

        [Test]
        public void Lifetime_FreshTrackerPerScope_ReproducesIdenticalSequences()
        {
            // Two Processes, each with a fresh tracker and equal scope identities, produce the same sequence.
            OccurrenceOrdinalTracker processOne = new();
            OccurrenceOrdinalTracker processTwo = new();

            int[] sequenceOne = { processOne.Next("Random ordering", "scope"), processOne.Next("Random ordering", "scope") };
            int[] sequenceTwo = { processTwo.Next("Random ordering", "scope"), processTwo.Next("Random ordering", "scope") };

            Assert.That(sequenceTwo, Is.EqualTo(sequenceOne));
            Assert.That(sequenceOne, Is.EqualTo(new[] { 1, 2 }));
        }

        [Test]
        public void Lifetime_ReusedTrackerAcrossScopes_DivergesFromFreshBaseline()
        {
            // A tracker reused across both Processes keeps advancing, so the second Process diverges from the
            // fresh-per-scope baseline of { 1, 2 }.
            OccurrenceOrdinalTracker shared = new();

            shared.Next("Random ordering", "scope");
            shared.Next("Random ordering", "scope");
            int[] secondProcess = { shared.Next("Random ordering", "scope"), shared.Next("Random ordering", "scope") };

            Assert.That(secondProcess, Is.EqualTo(new[] { 3, 4 }));
            Assert.That(secondProcess, Is.Not.EqualTo(new[] { 1, 2 }));
        }

        [Test]
        public void Snapshot_OfAFreshTracker_IsEmpty()
        {
            OccurrenceOrdinalTracker tracker = new();

            Assert.That(tracker.Snapshot().Count, Is.EqualTo(0));
        }

        [Test]
        public void Snapshot_ReportsTheHighestOrdinalIssuedPerPair()
        {
            OccurrenceOrdinalTracker tracker = new();
            tracker.Next("a", "x");
            tracker.Next("a", "x");
            tracker.Next("b", "y");

            IReadOnlyList<OrdinalCounter> counters = tracker.Snapshot();

            Assert.That(counters.Count, Is.EqualTo(2));
            Assert.That(counters[0], Is.EqualTo(new OrdinalCounter("a", "x", 2)));
            Assert.That(counters[1], Is.EqualTo(new OrdinalCounter("b", "y", 1)));
        }

        [Test]
        public void Snapshot_OrdersByPurposeThenContextRegardlessOfCallOrder()
        {
            // The same three pairs advanced in reverse order must snapshot in the same order, or two
            // equivalent Sessions would write different bytes for the same state.
            OccurrenceOrdinalTracker tracker = new();
            tracker.Next("b", "y");
            tracker.Next("a", "z");
            tracker.Next("a", "x");

            IReadOnlyList<OrdinalCounter> counters = tracker.Snapshot();

            Assert.That(counters.Count, Is.EqualTo(3));
            Assert.That(counters[0], Is.EqualTo(new OrdinalCounter("a", "x", 1)));
            Assert.That(counters[1], Is.EqualTo(new OrdinalCounter("a", "z", 1)));
            Assert.That(counters[2], Is.EqualTo(new OrdinalCounter("b", "y", 1)));
        }

        [Test]
        public void Snapshot_OrdersOrdinallyNotCaseInsensitively()
        {
            // Ordinal string semantics: upper case sorts before lower case, matching the pair key's own
            // comparison rather than a culture-aware one.
            OccurrenceOrdinalTracker tracker = new();
            tracker.Next("a", "scope");
            tracker.Next("A", "scope");

            IReadOnlyList<OrdinalCounter> counters = tracker.Snapshot();

            Assert.That(counters[0].SelectionPurpose, Is.EqualTo("A"));
            Assert.That(counters[1].SelectionPurpose, Is.EqualTo("a"));
        }

        [Test]
        public void Restore_ContinuesEachKeyFromItsCount()
        {
            OccurrenceOrdinalTracker tracker = OccurrenceOrdinalTracker.Restore(new[]
            {
                new OrdinalCounter("a", "x", 2),
                new OrdinalCounter("b", "y", 1)
            });

            Assert.That(tracker.Next("a", "x"), Is.EqualTo(3));
            Assert.That(tracker.Next("b", "y"), Is.EqualTo(2));
        }

        [Test]
        public void Restore_StartsAnUnseenKeyAtOne()
        {
            OccurrenceOrdinalTracker tracker = OccurrenceOrdinalTracker.Restore(new[]
            {
                new OrdinalCounter("a", "x", 2)
            });

            Assert.That(tracker.Next("c", "z"), Is.EqualTo(1));
        }

        [Test]
        public void Restore_OfAnEmptyListIsAFreshTracker()
        {
            OccurrenceOrdinalTracker tracker = OccurrenceOrdinalTracker.Restore(Array.Empty<OrdinalCounter>());

            Assert.That(tracker.Next("a", "x"), Is.EqualTo(1));
            Assert.That(tracker.Snapshot().Count, Is.EqualTo(1));
        }

        [Test]
        public void Restore_RoundTripsASnapshot()
        {
            OccurrenceOrdinalTracker original = new();
            original.Next("a", "x");
            original.Next("a", "x");
            original.Next("b", "y");

            IReadOnlyList<OrdinalCounter> restored = OccurrenceOrdinalTracker.Restore(original.Snapshot()).Snapshot();

            Assert.That(restored, Is.EqualTo(original.Snapshot()));
        }

        [Test]
        public void Restore_NullList_Throws()
        {
            Assert.Throws<ArgumentException>(() => OccurrenceOrdinalTracker.Restore(null));
        }

        [Test]
        public void Restore_DuplicateKey_Throws()
        {
            Assert.Throws<ArgumentException>(() => OccurrenceOrdinalTracker.Restore(new[]
            {
                new OrdinalCounter("a", "x", 1),
                new OrdinalCounter("a", "x", 2)
            }));
        }

        [Test]
        public void Restore_SamePurposeDifferentContext_IsNotADuplicate()
        {
            OccurrenceOrdinalTracker tracker = OccurrenceOrdinalTracker.Restore(new[]
            {
                new OrdinalCounter("a", "x", 1),
                new OrdinalCounter("a", "y", 1)
            });

            Assert.That(tracker.Snapshot().Count, Is.EqualTo(2));
        }

        [Test]
        public void Restore_CountBelowOne_Throws([Values(0, -1)] int count)
        {
            Assert.Throws<ArgumentException>(() => OccurrenceOrdinalTracker.Restore(new[]
            {
                new OrdinalCounter("a", "x", count)
            }));
        }

        [Test]
        public void Restore_EmptyOrNullPurpose_Throws([Values("", null)] string purpose)
        {
            Assert.Throws<ArgumentException>(() => OccurrenceOrdinalTracker.Restore(new[]
            {
                new OrdinalCounter(purpose, "x", 1)
            }));
        }

        [Test]
        public void Restore_EmptyOrNullContext_Throws([Values("", null)] string context)
        {
            Assert.Throws<ArgumentException>(() => OccurrenceOrdinalTracker.Restore(new[]
            {
                new OrdinalCounter("a", context, 1)
            }));
        }

        [Test]
        public void Restore_NullCounter_Throws()
        {
            Assert.Throws<ArgumentException>(() => OccurrenceOrdinalTracker.Restore(new OrdinalCounter[] { null }));
        }
    }
}
