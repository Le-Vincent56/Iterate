using NUnit.Framework;
using Iterate.Domain.Execution;
using Iterate.Domain.Trace;

namespace Iterate.Domain.Execution.Tests
{
    /// <summary>
    /// Tests the per-execution <see cref="RuntimeAdjacencyTracker"/>: the predecessor qualifies only
    /// when it is player-owned and actually increased Score (CAB-EVT-440); successful and rescued
    /// closures replace it while skipped, prevented, and cancelled closures leave the prior one
    /// intact (CAB-EVT-333/556/313/557); and an intervening successful non-qualifying unit — a Core
    /// line or a player unit that gained no Score — breaks adjacency by replacing it
    /// (CAB-EVT-281). Structure events, reactions, and source-less boundary interventions are
    /// transparent by absence: the tracker exposes no entry point for them.
    /// </summary>
    public sealed class RuntimeAdjacencyTrackerTests
    {
        [Test]
        public void FreshTracker_HasNoQualifyingPredecessor()
        {
            RuntimeAdjacencyTracker tracker = new RuntimeAdjacencyTracker();

            Assert.IsFalse(tracker.CurrentPredecessorQualifies);
        }

        [Test]
        public void SuccessfulPlayerScoreGain_Qualifies()
        {
            RuntimeAdjacencyTracker tracker = new RuntimeAdjacencyTracker();

            tracker.RecordCompletion(
                EventDisposition.Resolved,
                OwnershipClassification.PlayerOwned,
                true);

            Assert.IsTrue(tracker.CurrentPredecessorQualifies);
        }

        [Test]
        public void SuccessfulPlayerUnitWithoutScoreGain_ReplacesAndDisqualifies()
        {
            RuntimeAdjacencyTracker tracker = new RuntimeAdjacencyTracker();

            tracker.RecordCompletion(
                EventDisposition.Resolved,
                OwnershipClassification.PlayerOwned,
                true);
            tracker.RecordCompletion(
                EventDisposition.Resolved,
                OwnershipClassification.PlayerOwned,
                false);

            Assert.IsFalse(tracker.CurrentPredecessorQualifies);
        }

        [Test]
        public void SuccessfulCoreScoreGain_ReplacesAndDisqualifies()
        {
            RuntimeAdjacencyTracker tracker = new RuntimeAdjacencyTracker();

            tracker.RecordCompletion(
                EventDisposition.Resolved,
                OwnershipClassification.PlayerOwned,
                true);
            tracker.RecordCompletion(
                EventDisposition.Resolved,
                OwnershipClassification.CoreOwned,
                true);

            Assert.IsFalse(tracker.CurrentPredecessorQualifies);
        }

        [Test]
        public void RescuedPlayerScoreGain_Qualifies()
        {
            RuntimeAdjacencyTracker tracker = new RuntimeAdjacencyTracker();

            tracker.RecordCompletion(
                EventDisposition.Rescued,
                OwnershipClassification.PlayerOwned,
                true);

            Assert.IsTrue(tracker.CurrentPredecessorQualifies);
        }

        [Test]
        public void SkippedClosure_LeavesPriorPredecessorIntact()
        {
            RuntimeAdjacencyTracker tracker = new RuntimeAdjacencyTracker();

            tracker.RecordCompletion(
                EventDisposition.Resolved,
                OwnershipClassification.PlayerOwned,
                true);
            tracker.RecordCompletion(
                EventDisposition.Skipped,
                OwnershipClassification.PlayerOwned,
                false);

            Assert.IsTrue(tracker.CurrentPredecessorQualifies);
        }

        [Test]
        public void PreventedClosure_LeavesPriorPredecessorIntact()
        {
            RuntimeAdjacencyTracker tracker = new RuntimeAdjacencyTracker();

            tracker.RecordCompletion(
                EventDisposition.Resolved,
                OwnershipClassification.PlayerOwned,
                true);
            tracker.RecordCompletion(
                EventDisposition.Prevented,
                OwnershipClassification.PlayerOwned,
                false);

            Assert.IsTrue(tracker.CurrentPredecessorQualifies);
        }

        [Test]
        public void CancelledClosure_LeavesPriorPredecessorIntact()
        {
            RuntimeAdjacencyTracker tracker = new RuntimeAdjacencyTracker();

            tracker.RecordCompletion(
                EventDisposition.Resolved,
                OwnershipClassification.PlayerOwned,
                true);
            tracker.RecordCompletion(
                EventDisposition.Cancelled,
                OwnershipClassification.PlayerOwned,
                false);

            Assert.IsTrue(tracker.CurrentPredecessorQualifies);
        }

        [Test]
        public void FailedToQualifyClosure_LeavesPriorPredecessorIntact()
        {
            RuntimeAdjacencyTracker tracker = new RuntimeAdjacencyTracker();

            tracker.RecordCompletion(
                EventDisposition.Resolved,
                OwnershipClassification.PlayerOwned,
                true);
            tracker.RecordCompletion(
                EventDisposition.FailedToQualify,
                OwnershipClassification.PlayerOwned,
                false);

            Assert.IsTrue(tracker.CurrentPredecessorQualifies);
        }

        [Test]
        public void SkippedClosure_BeforeAnyPredecessor_StaysNonQualifying()
        {
            RuntimeAdjacencyTracker tracker = new RuntimeAdjacencyTracker();

            tracker.RecordCompletion(
                EventDisposition.Skipped,
                OwnershipClassification.PlayerOwned,
                false);

            Assert.IsFalse(tracker.CurrentPredecessorQualifies);
        }

        [Test]
        public void QualifyingUnitAfterNonQualifyingUnit_RestoresAdjacency()
        {
            RuntimeAdjacencyTracker tracker = new RuntimeAdjacencyTracker();

            tracker.RecordCompletion(
                EventDisposition.Resolved,
                OwnershipClassification.CoreOwned,
                true);
            tracker.RecordCompletion(
                EventDisposition.Resolved,
                OwnershipClassification.PlayerOwned,
                true);

            Assert.IsTrue(tracker.CurrentPredecessorQualifies);
        }

        [Test]
        public void SkippedBetweenQualifyingAndObserver_PreservesAdjacencyAcrossTheGap()
        {
            RuntimeAdjacencyTracker tracker = new RuntimeAdjacencyTracker();

            tracker.RecordCompletion(
                EventDisposition.Resolved,
                OwnershipClassification.PlayerOwned,
                true);
            tracker.RecordCompletion(
                EventDisposition.Skipped,
                OwnershipClassification.PlayerOwned,
                false);
            tracker.RecordCompletion(
                EventDisposition.Skipped,
                OwnershipClassification.PlayerOwned,
                false);

            Assert.IsTrue(tracker.CurrentPredecessorQualifies);
        }
    }
}
