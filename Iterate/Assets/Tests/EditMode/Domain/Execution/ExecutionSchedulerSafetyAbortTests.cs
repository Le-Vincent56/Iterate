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
    /// Tests the safety-abort path end to end through the scheduler's entry point on a Repeat whose
    /// iteration count drives the source-execution-unit ceiling. A Repeat of exactly the ceiling
    /// reaches it and is permitted; one more attempts a unit the contract prohibits, so the breach
    /// site throws the unwind signal, the entry point catches exactly that type, and abort
    /// finalization records the attempted occurrence, terminates the branch, and freezes an invalid,
    /// handoff-blocked record ending in the safety bookend instead of ordinary completion.
    /// The unit ceiling is the cheapest end-to-end breach on a Repeat-shaped arrangement: it needs
    /// one Structure and one Instruction, where a lineage-depth breach needs 33 distinct creator
    /// origins and a reaction breach 16,385 resolved reactions. Both of those are built — the
    /// added-execution boundary suite and the reaction-ceiling suite respectively.
    /// Every breach in this suite lands with **no unit open**, which is the ordinary case: the
    /// scheduler closes a unit before draining its descendants, so no two units are ever open at
    /// once and the frame stack is the causal-ancestor chain rather than the open-unit chain. The
    /// one-open-unit case — a reaction breaching inside the operation path, closing its unit with
    /// SafetyIntervention — is not constructible here, because the unit ceiling is reached long
    /// before the reaction ceiling on any Repeat-shaped arrangement. It lands in the
    /// reaction-ceiling suite, where five reaction instances per unit reach 16,385 reactions at
    /// 3,277 units, well inside the unit budget.
    /// </summary>
    public sealed class ExecutionSchedulerSafetyAbortTests
    {
        [Test]
        public void RepeatAtUnitCeiling_CompletesNormally()
        {
            ExecutionRecord record = Execute(RepeatRequest(SafetyCeilings.SourceExecutionUnitsPerExecution));

            Assert.AreEqual(ExecutionCompletionStatus.Completed, record.CompletionStatus);
            Assert.AreEqual(SafetyCeilings.SourceExecutionUnitsPerExecution, record.SafetyCounts.SourceExecutionUnits);
            Assert.AreEqual(ResultValidityStatus.Valid, record.ResultValidity);
            Assert.AreEqual(ResultReviewHandoffStatus.Eligible, record.HandoffStatus);
        }

        [Test]
        public void RepeatAtUnitCeiling_FinalizesApproachedNotAborted()
        {
            ExecutionRecord record = Execute(RepeatRequest(SafetyCeilings.SourceExecutionUnitsPerExecution));

            Assert.AreEqual(SafetyStatus.SafetyLimitApproached, record.SafetyStatus);
            Assert.AreEqual(1, CountSubtype(record, ExecutionEventSubtypes.SafetyLimitApproached));
            Assert.AreEqual(0, CountSubtype(record, ExecutionEventSubtypes.SafetyLimitReached));
        }

        [Test]
        public void RepeatBelowUnitCeiling_StaysNormalWithNoSafetyEvents()
        {
            ExecutionRecord record = Execute(RepeatRequest(4));

            Assert.AreEqual(SafetyStatus.Normal, record.SafetyStatus);
            Assert.AreEqual(0, CountSubtype(record, ExecutionEventSubtypes.SafetyLimitApproached));
            Assert.AreEqual(0, CountFamily(record, EventFamilies.Safety));
        }

        [Test]
        public void ApproachedEvent_NamesTheUnitLimitAtItsCeiling()
        {
            ExecutionRecord record = Execute(RepeatRequest(SafetyCeilings.SourceExecutionUnitsPerExecution));

            EventEvidence approached = SingleSubtype(record, ExecutionEventSubtypes.SafetyLimitApproached);
            SafetyAbortPayload payload = (SafetyAbortPayload)approached.Payload;

            Assert.AreEqual(EventFamilies.Safety, approached.Family);
            Assert.AreEqual(1, payload.BreachedLimits.Count);
            Assert.AreEqual(
                SafetyAbortSignal.LimitName(SafetyLimitFlags.SourceExecutionUnits),
                payload.BreachedLimits[0].LimitName);
            Assert.AreEqual(
                SafetyCeilings.SourceExecutionUnitsPerExecution,
                payload.BreachedLimits[0].CountAtBreach);
        }

        [Test]
        public void ApproachedStatus_IsStickyAcrossLaterEvents()
        {
            ExecutionRecord record = Execute(RepeatRequest(SafetyCeilings.SourceExecutionUnitsPerExecution));

            int approachedIndex = IndexOfSubtype(record, ExecutionEventSubtypes.SafetyLimitApproached);

            Assert.Greater(approachedIndex, 0);
            Assert.AreEqual(SafetyStatus.Normal, record.Events[approachedIndex - 1].Evidence.SafetyStatus);
            for (int i = approachedIndex; i < record.Events.Count; i++)
            {
                Assert.AreEqual(SafetyStatus.SafetyLimitApproached, record.Events[i].Evidence.SafetyStatus);
            }
        }

        [Test]
        public void RepeatPastUnitCeiling_FinalizesSafetyAborted()
        {
            ExecutionRecord record = Execute(RepeatRequest(SafetyCeilings.SourceExecutionUnitsPerExecution + 1));

            Assert.AreEqual(ExecutionCompletionStatus.SafetyAborted, record.CompletionStatus);
            Assert.AreEqual(SafetyStatus.SafetyAborted, record.SafetyStatus);
        }

        [Test]
        public void AbortedRecord_IsInvalidAndHandoffBlocked()
        {
            ExecutionRecord record = Execute(RepeatRequest(SafetyCeilings.SourceExecutionUnitsPerExecution + 1));

            Assert.AreEqual(ResultValidityStatus.Invalid, record.ResultValidity);
            Assert.AreEqual(ResultReviewHandoffStatus.Blocked, record.HandoffStatus);
        }

        [Test]
        public void AbortedRecord_NeverEmitsExecutionCompleted()
        {
            ExecutionRecord record = Execute(RepeatRequest(SafetyCeilings.SourceExecutionUnitsPerExecution + 1));

            Assert.AreEqual(0, CountSubtype(record, ExecutionEventSubtypes.ExecutionCompleted));
            Assert.AreEqual(1, CountSubtype(record, ExecutionEventSubtypes.ExecutionSafetyAborted));
            Assert.AreEqual(
                ExecutionEventSubtypes.ExecutionSafetyAborted,
                record.Events[record.Events.Count - 1].Evidence.Subtype);
        }

        [Test]
        public void AbortSequence_IsReachedThenBranchTerminatedThenBookend()
        {
            ExecutionRecord record = Execute(RepeatRequest(SafetyCeilings.SourceExecutionUnitsPerExecution + 1));

            int reached = IndexOfSubtype(record, ExecutionEventSubtypes.SafetyLimitReached);
            int terminated = IndexOfSubtype(record, ExecutionEventSubtypes.CausalBranchTerminated);
            int aborted = IndexOfSubtype(record, ExecutionEventSubtypes.ExecutionSafetyAborted);

            Assert.Greater(reached, 0);
            Assert.AreEqual(reached + 1, terminated);
            Assert.AreEqual(terminated + 1, aborted);
            Assert.AreEqual(EventFamilies.Safety, record.Events[reached].Evidence.Family);
            Assert.AreEqual(EventFamilies.Safety, record.Events[terminated].Evidence.Family);
        }

        [Test]
        public void BranchTerminated_IsCausedByTheLimitReachedEvent()
        {
            ExecutionRecord record = Execute(RepeatRequest(SafetyCeilings.SourceExecutionUnitsPerExecution + 1));

            int reached = IndexOfSubtype(record, ExecutionEventSubtypes.SafetyLimitReached);
            EventEvidence terminated = SingleSubtype(record, ExecutionEventSubtypes.CausalBranchTerminated);

            Assert.AreEqual(record.Events[reached].Identity, terminated.CausingEvent);
        }

        [Test]
        public void LimitReachedEvent_CarriesTheFullAbortPayload()
        {
            ExecutionRecord record = Execute(RepeatRequest(SafetyCeilings.SourceExecutionUnitsPerExecution + 1));

            EventEvidence reached = SingleSubtype(record, ExecutionEventSubtypes.SafetyLimitReached);
            SafetyAbortPayload payload = (SafetyAbortPayload)reached.Payload;

            Assert.AreEqual(1, payload.BreachedLimits.Count);
            Assert.AreEqual(
                SafetyAbortSignal.LimitName(SafetyLimitFlags.SourceExecutionUnits),
                payload.BreachedLimits[0].LimitName);
            Assert.AreEqual(SafetyCeilings.SourceExecutionUnitsPerExecution, payload.BreachedLimits[0].Ceiling);
            Assert.AreEqual(SafetyCeilings.SourceExecutionUnitsPerExecution, payload.BreachedLimits[0].CountAtBreach);
            Assert.AreEqual(
                SafetyAbortSignal.ComposeOccurrenceIdentity(
                    EventFamilies.Source,
                    ExecutionEventSubtypes.SourceExecutionStarted,
                    "P2"),
                payload.OverLimitOccurrenceIdentity);
        }

        [Test]
        public void AbortWithNoUnitOpen_LeavesTheSafetyEventsUnitLess()
        {
            ExecutionRecord record = Execute(RepeatRequest(SafetyCeilings.SourceExecutionUnitsPerExecution + 1));

            Assert.IsNull(SingleSubtype(record, ExecutionEventSubtypes.SafetyLimitReached).ContainingUnit);
            Assert.IsNull(SingleSubtype(record, ExecutionEventSubtypes.CausalBranchTerminated).ContainingUnit);
            Assert.IsNull(SingleSubtype(record, ExecutionEventSubtypes.ExecutionSafetyAborted).ContainingUnit);
        }

        [Test]
        public void AbortWithNoUnitOpen_ClosesNoUnitWithSafetyIntervention()
        {
            ExecutionRecord record = Execute(RepeatRequest(SafetyCeilings.SourceExecutionUnitsPerExecution + 1));

            int intervened = 0;
            for (int i = 0; i < record.Units.Count; i++)
            {
                if (record.Units[i].Closure.Status == UnitClosureStatus.SafetyIntervention)
                    intervened++;
            }

            Assert.AreEqual(0, intervened);
        }

        [Test]
        public void PreBreachUnits_KeepTheirNormalCompletionEvidence()
        {
            ExecutionRecord record = Execute(RepeatRequest(SafetyCeilings.SourceExecutionUnitsPerExecution + 1));

            Assert.AreEqual(SafetyCeilings.SourceExecutionUnitsPerExecution, record.Units.Count);
            for (int i = 0; i < record.Units.Count; i++)
            {
                Assert.AreEqual(UnitClosureStatus.NormalCompletion, record.Units[i].Closure.Status);
            }
        }

        [Test]
        public void OverLimitUnit_IsNeverCreated()
        {
            ExecutionRecord record = Execute(RepeatRequest(SafetyCeilings.SourceExecutionUnitsPerExecution + 1));

            Assert.AreEqual(SafetyCeilings.SourceExecutionUnitsPerExecution, record.SafetyCounts.SourceExecutionUnits);
            Assert.AreEqual(SafetyCeilings.SourceExecutionUnitsPerExecution, record.Units.Count);
        }

        [Test]
        public void AbortEvents_CarryTheAbortedSafetyStatus()
        {
            ExecutionRecord record = Execute(RepeatRequest(SafetyCeilings.SourceExecutionUnitsPerExecution + 1));

            int reached = IndexOfSubtype(record, ExecutionEventSubtypes.SafetyLimitReached);

            for (int i = reached; i < record.Events.Count; i++)
            {
                Assert.AreEqual(SafetyStatus.SafetyAborted, record.Events[i].Evidence.SafetyStatus);
            }
        }

        [Test]
        public void AbortLeavesTheBuilderReusable_SecondExecutionCompletesNormally()
        {
            ExecutionScheduler scheduler = new ExecutionScheduler(new ExecutionTraceBuilder());

            ExecutionRecord aborted = scheduler.Execute(
                RepeatRequest(SafetyCeilings.SourceExecutionUnitsPerExecution + 1));
            ExecutionRecord next = scheduler.Execute(RepeatRequest(3));

            Assert.AreEqual(ExecutionCompletionStatus.SafetyAborted, aborted.CompletionStatus);
            Assert.AreEqual(ExecutionCompletionStatus.Completed, next.CompletionStatus);
            Assert.AreEqual(SafetyStatus.Normal, next.SafetyStatus);
            Assert.AreEqual(3, next.SafetyCounts.SourceExecutionUnits);
        }

        [Test]
        public void AbortIsDeterministic_TwoRunsAreStructurallyEqual()
        {
            ExecutionScheduler scheduler = new ExecutionScheduler(new ExecutionTraceBuilder());
            ExecutionRequest request = RepeatRequest(SafetyCeilings.SourceExecutionUnitsPerExecution + 1);

            ExecutionRecord first = scheduler.Execute(request);
            ExecutionRecord second = scheduler.Execute(request);

            Assert.AreEqual(first, second);
        }

        /// <summary>
        /// A Repeat over one contained <c>Signal += 1</c> Instruction, iterating the given count:
        /// Structure #50 header at position 1, contained Instruction #51 at position 2. Each
        /// iteration opens exactly one source-execution unit, so the iteration count is the unit
        /// count.
        /// </summary>
        /// <param name="count">The authored iteration count.</param>
        /// <returns>The assembled request.</returns>
        private static ExecutionRequest RepeatRequest(int count)
        {
            StructureInstance structure = SchedulerFixtures.RepeatStructure(50, count, 2);
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForStructureHeader(new SourcePosition(1), structure),
                SourceSlot.ForContainedInstruction(new SourcePosition(2), structure, SchedulerFixtures.SignalAddInstance(51, 1))
            });

            return SchedulerFixtures.RequestOver(arrangement, SchedulerFixtures.ZeroState(), new InstanceIDSource());
        }

        /// <summary>
        /// Runs one request on a fresh scheduler.
        /// </summary>
        /// <param name="request">The request to execute.</param>
        /// <returns>The frozen record.</returns>
        private static ExecutionRecord Execute(ExecutionRequest request)
        {
            return new ExecutionScheduler(new ExecutionTraceBuilder()).Execute(request);
        }

        /// <summary>
        /// Counts the events carrying one subtype.
        /// </summary>
        /// <param name="record">The frozen record.</param>
        /// <param name="subtype">The subtype to count.</param>
        /// <returns>The number of matching events.</returns>
        private static int CountSubtype(ExecutionRecord record, string subtype)
        {
            int count = 0;
            for (int i = 0; i < record.Events.Count; i++)
            {
                if (record.Events[i].Evidence.Subtype == subtype)
                    count++;
            }

            return count;
        }

        /// <summary>
        /// Counts the events carrying one family.
        /// </summary>
        /// <param name="record">The frozen record.</param>
        /// <param name="family">The family to count.</param>
        /// <returns>The number of matching events.</returns>
        private static int CountFamily(ExecutionRecord record, string family)
        {
            int count = 0;
            for (int i = 0; i < record.Events.Count; i++)
            {
                if (record.Events[i].Evidence.Family == family)
                    count++;
            }

            return count;
        }

        /// <summary>
        /// The zero-based index of the first event carrying one subtype, or minus one.
        /// </summary>
        /// <param name="record">The frozen record.</param>
        /// <param name="subtype">The subtype to find.</param>
        /// <returns>The index, or minus one when absent.</returns>
        private static int IndexOfSubtype(ExecutionRecord record, string subtype)
        {
            for (int i = 0; i < record.Events.Count; i++)
            {
                if (record.Events[i].Evidence.Subtype == subtype)
                    return i;
            }

            return -1;
        }

        /// <summary>
        /// The single event carrying one subtype, asserting exactly one exists.
        /// </summary>
        /// <param name="record">The frozen record.</param>
        /// <param name="subtype">The subtype to find.</param>
        /// <returns>The matching evidence.</returns>
        private static EventEvidence SingleSubtype(ExecutionRecord record, string subtype)
        {
            Assert.AreEqual(1, CountSubtype(record, subtype));
            return record.Events[IndexOfSubtype(record, subtype)].Evidence;
        }
    }
}
