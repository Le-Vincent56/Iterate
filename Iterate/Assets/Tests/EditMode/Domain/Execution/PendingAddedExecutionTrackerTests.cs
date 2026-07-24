using System;
using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Domain.Compilation;
using Iterate.Domain.Content;
using Iterate.Domain.Execution;
using Iterate.Domain.Trace;
using Iterate.Domain.Values;

namespace Iterate.Domain.Execution.Tests
{
    /// <summary>
    /// Tests the pending added-execution tracker's state machine: a successfully closed locked host
    /// matures its request, an unsuccessful closure cancels it with the locked-host reason, an
    /// execution that closes with requests still pending cancels them with the execution-close
    /// reason, and both reason strings are owned here rather than at the scheduler's emission sites.
    /// </summary>
    public sealed class PendingAddedExecutionTrackerTests
    {
        private static readonly InstructionDefinition _instructionDefinition = new(
            new InstructionID("WB-INS-002"),
            "rules",
            "Add 2 to Value",
            ContentCategory.Instruction,
            Rarity.Common,
            Array.Empty<string>(),
            1,
            null,
            null,
            Array.Empty<string>());

        [Test]
        public void TryClose_ResolvedHost_Matures()
        {
            PendingAddedExecutionTracker tracker = new PendingAddedExecutionTracker();
            AddedExecutionRequest request = Pending(60);
            tracker.Establish(request);

            PendingResolution resolution = tracker.TryClose(
                new InstanceID(60),
                EventDisposition.Resolved,
                out AddedExecutionRequest matured,
                out CancelledRequest cancelled);

            Assert.AreEqual(PendingResolution.Matured, resolution);
            Assert.AreSame(request, matured);
            Assert.IsNull(cancelled);
        }

        [Test]
        public void TryClose_RescuedHost_Matures()
        {
            PendingAddedExecutionTracker tracker = new PendingAddedExecutionTracker();
            tracker.Establish(Pending(60));

            PendingResolution resolution = tracker.TryClose(
                new InstanceID(60),
                EventDisposition.Rescued,
                out AddedExecutionRequest matured,
                out CancelledRequest cancelled);

            Assert.AreEqual(PendingResolution.Matured, resolution);
            Assert.IsNotNull(matured);
            Assert.IsNull(cancelled);
        }

        [Test]
        public void TryClose_SkippedHost_CancelsWithLockedHostReason()
        {
            PendingAddedExecutionTracker tracker = new PendingAddedExecutionTracker();
            AddedExecutionRequest request = Pending(60);
            tracker.Establish(request);

            PendingResolution resolution = tracker.TryClose(
                new InstanceID(60),
                EventDisposition.Skipped,
                out AddedExecutionRequest matured,
                out CancelledRequest cancelled);

            Assert.AreEqual(PendingResolution.Cancelled, resolution);
            Assert.IsNull(matured);
            Assert.AreSame(request, cancelled.Request);
            Assert.AreEqual("LOCKED_HOST_UNSUCCESSFUL:SKIPPED", cancelled.Reason);
        }

        [Test]
        public void TryClose_PreventedHost_CancelsWithItsOwnDispositionToken()
        {
            PendingAddedExecutionTracker tracker = new PendingAddedExecutionTracker();
            tracker.Establish(Pending(60));

            tracker.TryClose(
                new InstanceID(60),
                EventDisposition.Prevented,
                out AddedExecutionRequest matured,
                out CancelledRequest cancelled);

            Assert.AreEqual("LOCKED_HOST_UNSUCCESSFUL:PREVENTED", cancelled.Reason);
        }

        [Test]
        public void TryClose_UnknownHost_ReturnsNone()
        {
            PendingAddedExecutionTracker tracker = new PendingAddedExecutionTracker();
            tracker.Establish(Pending(60));

            PendingResolution resolution = tracker.TryClose(
                new InstanceID(99),
                EventDisposition.Resolved,
                out AddedExecutionRequest matured,
                out CancelledRequest cancelled);

            Assert.AreEqual(PendingResolution.None, resolution);
            Assert.IsNull(matured);
            Assert.IsNull(cancelled);
        }

        [Test]
        public void TryClose_Twice_TheTakenRequestIsGone()
        {
            PendingAddedExecutionTracker tracker = new PendingAddedExecutionTracker();
            tracker.Establish(Pending(60));
            tracker.TryClose(new InstanceID(60), EventDisposition.Resolved, out AddedExecutionRequest _, out CancelledRequest _);

            PendingResolution second = tracker.TryClose(
                new InstanceID(60),
                EventDisposition.Resolved,
                out AddedExecutionRequest matured,
                out CancelledRequest cancelled);

            Assert.AreEqual(PendingResolution.None, second);
            Assert.IsNull(matured);
            Assert.IsNull(cancelled);
        }

        [Test]
        public void DrainPending_ReturnsTheRestWithTheExecutionCloseReason()
        {
            PendingAddedExecutionTracker tracker = new PendingAddedExecutionTracker();
            AddedExecutionRequest first = Pending(60);
            AddedExecutionRequest second = Pending(61);
            tracker.Establish(first);
            tracker.Establish(second);

            IReadOnlyList<CancelledRequest> drained = tracker.DrainPending();

            Assert.AreEqual(2, drained.Count);
            Assert.AreSame(first, drained[0].Request);
            Assert.AreSame(second, drained[1].Request);
            Assert.AreEqual("EXECUTION_CLOSED_PENDING", drained[0].Reason);
            Assert.AreEqual("EXECUTION_CLOSED_PENDING", drained[1].Reason);
        }

        [Test]
        public void DrainPending_EmptiesTheTracker()
        {
            PendingAddedExecutionTracker tracker = new PendingAddedExecutionTracker();
            tracker.Establish(Pending(60));
            tracker.DrainPending();

            IReadOnlyList<CancelledRequest> second = tracker.DrainPending();

            Assert.AreEqual(0, second.Count);
        }

        [Test]
        public void DrainPending_AfterMaturation_ExcludesTheTakenRequest()
        {
            PendingAddedExecutionTracker tracker = new PendingAddedExecutionTracker();
            tracker.Establish(Pending(60));
            tracker.Establish(Pending(61));
            tracker.TryClose(new InstanceID(60), EventDisposition.Resolved, out AddedExecutionRequest _, out CancelledRequest _);

            IReadOnlyList<CancelledRequest> drained = tracker.DrainPending();

            Assert.AreEqual(1, drained.Count);
            Assert.AreEqual(new InstanceID(61), drained[0].Request.LockedSlot.Instruction.InstanceID);
        }

        [Test]
        public void Establish_NullRequest_Throws()
        {
            PendingAddedExecutionTracker tracker = new PendingAddedExecutionTracker();

            Assert.Throws<ArgumentException>(() => tracker.Establish(null));
        }

        [Test]
        public void Establish_RequestWithParentUnit_Throws()
        {
            PendingAddedExecutionTracker tracker = new PendingAddedExecutionTracker();
            AddedExecutionRequest resolved = Pending(60) with { ParentUnit = new RuntimeUnitID(3) };

            Assert.Throws<ArgumentException>(() => tracker.Establish(resolved));
        }

        [Test]
        public void CancelledRequest_NullRequest_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new CancelledRequest(null, "EXECUTION_CLOSED_PENDING"));
        }

        [Test]
        public void CancelledRequest_EmptyReason_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new CancelledRequest(Pending(60), string.Empty));
        }

        /// <summary>
        /// Builds a pending BRANCH-PREDICTOR-shaped request locking the given contained Instruction.
        /// </summary>
        /// <param name="hostInstance">The locked host instance's identity value.</param>
        /// <returns>The pending request.</returns>
        private static AddedExecutionRequest Pending(int hostInstance)
        {
            InstructionInstance instruction = new(new InstanceID(hostInstance), _instructionDefinition, null);
            SourceSlot slot = SourceSlot.ForInstruction(new SourcePosition(2), instruction);

            return new AddedExecutionRequest(
                "WB-DEP-010:0#800@e12",
                new InstanceID(800),
                "WB-DEP-010",
                new TraceEventID(12),
                new TraceEventID(13),
                slot,
                null,
                EffectOriginLineage.Empty.Append(new InstanceID(800)),
                1,
                null);
        }
    }
}
