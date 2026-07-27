using NUnit.Framework;
using Iterate.Domain.Execution;
using Iterate.Domain.Trace;
using Iterate.Domain.Values;

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
                new RuntimeUnitID(1),
                EventDisposition.Resolved,
                OwnershipClassification.PlayerOwned,
                new InstanceID(10),
                true);

            Assert.IsTrue(tracker.CurrentPredecessorQualifies);
        }

        [Test]
        public void SuccessfulPlayerUnitWithoutScoreGain_ReplacesAndDisqualifies()
        {
            RuntimeAdjacencyTracker tracker = new RuntimeAdjacencyTracker();

            tracker.RecordCompletion(
                new RuntimeUnitID(1),
                EventDisposition.Resolved,
                OwnershipClassification.PlayerOwned,
                new InstanceID(10),
                true);
            tracker.RecordCompletion(
                new RuntimeUnitID(2),
                EventDisposition.Resolved,
                OwnershipClassification.PlayerOwned,
                new InstanceID(11),
                false);

            Assert.IsFalse(tracker.CurrentPredecessorQualifies);
        }

        [Test]
        public void SuccessfulCoreScoreGain_ReplacesAndDisqualifies()
        {
            RuntimeAdjacencyTracker tracker = new RuntimeAdjacencyTracker();

            tracker.RecordCompletion(
                new RuntimeUnitID(1),
                EventDisposition.Resolved,
                OwnershipClassification.PlayerOwned,
                new InstanceID(10),
                true);
            tracker.RecordCompletion(
                new RuntimeUnitID(2),
                EventDisposition.Resolved,
                OwnershipClassification.CoreOwned,
                null,
                true);

            Assert.IsFalse(tracker.CurrentPredecessorQualifies);
        }

        [Test]
        public void RescuedPlayerScoreGain_Qualifies()
        {
            RuntimeAdjacencyTracker tracker = new RuntimeAdjacencyTracker();

            tracker.RecordCompletion(
                new RuntimeUnitID(1),
                EventDisposition.Rescued,
                OwnershipClassification.PlayerOwned,
                new InstanceID(10),
                true);

            Assert.IsTrue(tracker.CurrentPredecessorQualifies);
        }

        [Test]
        public void SkippedClosure_LeavesPriorPredecessorIntact()
        {
            RuntimeAdjacencyTracker tracker = new RuntimeAdjacencyTracker();

            tracker.RecordCompletion(
                new RuntimeUnitID(1),
                EventDisposition.Resolved,
                OwnershipClassification.PlayerOwned,
                new InstanceID(10),
                true);
            tracker.RecordCompletion(
                new RuntimeUnitID(2),
                EventDisposition.Skipped,
                OwnershipClassification.PlayerOwned,
                new InstanceID(11),
                false);

            Assert.IsTrue(tracker.CurrentPredecessorQualifies);
        }

        [Test]
        public void PreventedClosure_LeavesPriorPredecessorIntact()
        {
            RuntimeAdjacencyTracker tracker = new RuntimeAdjacencyTracker();

            tracker.RecordCompletion(
                new RuntimeUnitID(1),
                EventDisposition.Resolved,
                OwnershipClassification.PlayerOwned,
                new InstanceID(10),
                true);
            tracker.RecordCompletion(
                new RuntimeUnitID(2),
                EventDisposition.Prevented,
                OwnershipClassification.PlayerOwned,
                new InstanceID(11),
                false);

            Assert.IsTrue(tracker.CurrentPredecessorQualifies);
        }

        [Test]
        public void CancelledClosure_LeavesPriorPredecessorIntact()
        {
            RuntimeAdjacencyTracker tracker = new RuntimeAdjacencyTracker();

            tracker.RecordCompletion(
                new RuntimeUnitID(1),
                EventDisposition.Resolved,
                OwnershipClassification.PlayerOwned,
                new InstanceID(10),
                true);
            tracker.RecordCompletion(
                new RuntimeUnitID(2),
                EventDisposition.Cancelled,
                OwnershipClassification.PlayerOwned,
                new InstanceID(11),
                false);

            Assert.IsTrue(tracker.CurrentPredecessorQualifies);
        }

        [Test]
        public void FailedToQualifyClosure_LeavesPriorPredecessorIntact()
        {
            RuntimeAdjacencyTracker tracker = new RuntimeAdjacencyTracker();

            tracker.RecordCompletion(
                new RuntimeUnitID(1),
                EventDisposition.Resolved,
                OwnershipClassification.PlayerOwned,
                new InstanceID(10),
                true);
            tracker.RecordCompletion(
                new RuntimeUnitID(2),
                EventDisposition.FailedToQualify,
                OwnershipClassification.PlayerOwned,
                new InstanceID(11),
                false);

            Assert.IsTrue(tracker.CurrentPredecessorQualifies);
        }

        [Test]
        public void SkippedClosure_BeforeAnyPredecessor_StaysNonQualifying()
        {
            RuntimeAdjacencyTracker tracker = new RuntimeAdjacencyTracker();

            tracker.RecordCompletion(
                new RuntimeUnitID(1),
                EventDisposition.Skipped,
                OwnershipClassification.PlayerOwned,
                new InstanceID(10),
                false);

            Assert.IsFalse(tracker.CurrentPredecessorQualifies);
        }

        [Test]
        public void QualifyingUnitAfterNonQualifyingUnit_RestoresAdjacency()
        {
            RuntimeAdjacencyTracker tracker = new RuntimeAdjacencyTracker();

            tracker.RecordCompletion(
                new RuntimeUnitID(1),
                EventDisposition.Resolved,
                OwnershipClassification.CoreOwned,
                null,
                true);
            tracker.RecordCompletion(
                new RuntimeUnitID(2),
                EventDisposition.Resolved,
                OwnershipClassification.PlayerOwned,
                new InstanceID(11),
                true);

            Assert.IsTrue(tracker.CurrentPredecessorQualifies);
        }

        [Test]
        public void SkippedBetweenQualifyingAndObserver_PreservesAdjacencyAcrossTheGap()
        {
            RuntimeAdjacencyTracker tracker = new RuntimeAdjacencyTracker();

            tracker.RecordCompletion(
                new RuntimeUnitID(1),
                EventDisposition.Resolved,
                OwnershipClassification.PlayerOwned,
                new InstanceID(10),
                true);
            tracker.RecordCompletion(
                new RuntimeUnitID(2),
                EventDisposition.Skipped,
                OwnershipClassification.PlayerOwned,
                new InstanceID(11),
                false);
            tracker.RecordCompletion(
                new RuntimeUnitID(3),
                EventDisposition.Skipped,
                OwnershipClassification.PlayerOwned,
                new InstanceID(12),
                false);

            Assert.IsTrue(tracker.CurrentPredecessorQualifies);
        }
    }
}
