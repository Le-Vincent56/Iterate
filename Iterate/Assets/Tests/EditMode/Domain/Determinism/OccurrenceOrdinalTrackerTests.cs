using System;
using NUnit.Framework;
using Iterate.Domain.Determinism;

namespace Iterate.Domain.Determinism.Tests
{
    /// <summary>
    /// Tests the caller-owned <see cref="OccurrenceOrdinalTracker"/> (CAB-EVT-836/837/838): one-based
    /// ordinals per (purpose, scope) pair, pair independence, empty-argument rejection, and the lifetime
    /// contract — fresh trackers per scope reproduce identical ordinal sequences, while a tracker reused
    /// across scopes diverges from that baseline.
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
    }
}
