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
    /// Tests the counting clauses of the source-execution-unit boundary, the transformation
    /// boundary, safety-abort invalidity, and deterministic safety replay — the parts of
    /// CAB-SAFE-155, CAB-SAFE-157, CAB-SAFE-159 and CAB-SAFE-160 that do not need a ceiling-sized
    /// fixture. The two ceiling-sized boundaries live in the added-execution and reaction suites,
    /// and the unit ceiling's own 4,096/4,097 pair is already pinned by the safety-abort suite.
    /// Two honest gaps are recorded here rather than faked.
    /// First, CAB-SAFE-157 has no end-to-end fixture at this content. The transformation re-anchor
    /// moved counting off the modification band, and the scheduler's producers are now exactly two:
    /// a Process-rule intervention that skips a pending operation, and a rescue that transforms a
    /// committed skip back into a resolution — both declared transformations. They do not make the
    /// ceiling reachable, because one pending operation admits at most one of each and the only
    /// rescuer is once per execution, so no pending operation can carry more than two of the
    /// thirty-three a breach needs. The boundary therefore stays pinned at the tallies seam, which
    /// is where the whole rule can be honestly exercised; the evaluator itself is separately covered
    /// by the tallies evaluation suite, and the three cases here exist so CAB-SAFE-157's own clauses
    /// are traceable to named tests.
    /// Second, CAB-SAFE-159 names five consequences, and only two of them have an engine-side
    /// surface today: the execution produces no valid current output, and it cannot enter ordinary
    /// Result Review. Submission, tier rewards, and approved-maximum recording belong to systems
    /// that do not exist yet; the engine's whole contribution to all three is the invalid,
    /// handoff-blocked frozen record asserted below, and no test here pretends otherwise.
    /// </summary>
    public sealed class ExecutionSchedulerSafetyContractTests
    {
        [Test]
        public void SkippedActivatedUnits_CountTowardTheUnitCeiling()
        {
            ExecutionRecord record = Execute(SkippedConditionRequest(new List<DependencyInstance>()));

            Assert.AreEqual(ExecutionCompletionStatus.Completed, record.CompletionStatus);
            Assert.AreEqual(2, record.SafetyCounts.SourceExecutionUnits);
            Assert.AreEqual(2, record.Units.Count);
            Assert.AreEqual(EventDisposition.Skipped, record.Units[0].Closure.FinalDisposition);
            Assert.AreEqual(EventDisposition.Resolved, record.Units[1].Closure.FinalDisposition);
        }

        [Test]
        public void RescuedUnit_IsNotDoubleCounted()
        {
            ExecutionRecord record = Execute(SkippedConditionRequest(
                new List<DependencyInstance> { SchedulerFixtures.SafeModeInstance(70) }));

            Assert.AreEqual(ExecutionCompletionStatus.Completed, record.CompletionStatus);
            Assert.AreEqual(2, record.SafetyCounts.SourceExecutionUnits);
            Assert.AreEqual(2, record.Units.Count);
            Assert.AreEqual(EventDisposition.Rescued, record.Units[0].Closure.FinalDisposition);
            Assert.AreEqual(EventDisposition.Skipped, record.Units[0].Closure.OriginalDisposition);
        }

        [Test]
        public void RescuedSkip_RecordsOneTransformation()
        {
            ExecutionRecord record = Execute(SkippedConditionRequest(
                new List<DependencyInstance> { SchedulerFixtures.SafeModeInstance(70) }));

            Assert.AreEqual(
                1,
                record.SafetyCounts.OperationTransformations,
                "A rescue changes the operation's governed disposition, which is a declared transformation.");
        }

        [Test]
        public void UnrescuedSkip_RecordsNoTransformation()
        {
            ExecutionRecord record = Execute(SkippedConditionRequest(new List<DependencyInstance>()));

            Assert.AreEqual(
                0,
                record.SafetyCounts.OperationTransformations,
                "A skip that no rule caused and no rescuer transformed leaves the count untouched.");
        }

        [Test]
        public void TransformationSeam_Permits32TransformationsForOnePendingOperation()
        {
            ExecutionSafetyTallies tallies = new ExecutionSafetyTallies();
            tallies.BeginPendingOperation();

            for (int i = 0; i < SafetyCeilings.TransformationsPerPendingOperation; i++)
            {
                Assert.IsTrue(tallies.PreflightTransformation());
                tallies.RecordTransformation();
            }

            Assert.AreEqual(
                SafetyCeilings.TransformationsPerPendingOperation,
                tallies.TransformationsOnPendingOperation);
            Assert.AreEqual(
                SafetyCeilings.TransformationsPerPendingOperation,
                tallies.TransformationHighWater);
        }

        [Test]
        public void TransformationSeam_Attempted33TriggersSafetyIntervention()
        {
            ExecutionSafetyTallies tallies = new ExecutionSafetyTallies();
            tallies.BeginPendingOperation();
            for (int i = 0; i < SafetyCeilings.TransformationsPerPendingOperation; i++)
            {
                tallies.RecordTransformation();
            }

            IReadOnlyList<BreachedLimit> breached = tallies.EvaluateTransformation();

            Assert.IsNotNull(breached);
            Assert.AreEqual(1, breached.Count);
            Assert.AreEqual(
                SafetyAbortSignal.LimitName(SafetyLimitFlags.TransformationsOnPendingOperation),
                breached[0].LimitName);
            Assert.AreEqual(SafetyCeilings.TransformationsPerPendingOperation, breached[0].Ceiling);
            Assert.AreEqual(
                SafetyCeilings.TransformationsPerPendingOperation,
                breached[0].CountAtBreach);
        }

        [Test]
        public void TransformationSeam_NewPendingOperationReceivesASeparateCount()
        {
            ExecutionSafetyTallies tallies = new ExecutionSafetyTallies();
            tallies.BeginPendingOperation();
            for (int i = 0; i < SafetyCeilings.TransformationsPerPendingOperation; i++)
            {
                tallies.RecordTransformation();
            }

            tallies.BeginPendingOperation();

            Assert.AreEqual(0, tallies.TransformationsOnPendingOperation);
            Assert.IsTrue(tallies.PreflightTransformation());
            Assert.IsNull(tallies.EvaluateTransformation());
            Assert.AreEqual(
                SafetyCeilings.TransformationsPerPendingOperation,
                tallies.TransformationHighWater);
        }

        [Test]
        public void SafetyAbortedExecution_ProducesNoValidCurrentOutput()
        {
            ExecutionRecord record = Execute(AbortingRequest());

            Assert.AreEqual(ExecutionCompletionStatus.SafetyAborted, record.CompletionStatus);
            Assert.AreEqual(ResultValidityStatus.Invalid, record.ResultValidity);
        }

        [Test]
        public void SafetyAbortedExecution_CannotEnterOrdinaryResultReview()
        {
            ExecutionRecord record = Execute(AbortingRequest());

            Assert.AreEqual(ResultReviewHandoffStatus.Blocked, record.HandoffStatus);
            Assert.AreEqual(0, CountSubtype(record, ExecutionEventSubtypes.ExecutionCompleted));
            Assert.AreEqual(1, CountSubtype(record, ExecutionEventSubtypes.ExecutionSafetyAborted));
        }

        [Test]
        public void SafetyAbortedExecution_CarriesTheAbortedStatusOnEveryPostBreachEvent()
        {
            ExecutionRecord record = Execute(AbortingRequest());

            int reached = IndexOfSubtype(record, ExecutionEventSubtypes.SafetyLimitReached);

            Assert.Greater(reached, 0);
            for (int i = reached; i < record.Events.Count; i++)
            {
                Assert.AreEqual(SafetyStatus.SafetyAborted, record.Events[i].Evidence.SafetyStatus);
            }
        }

        [Test]
        public void SafetyAbortedExecution_PreservesPreBreachClosureEvidence()
        {
            ExecutionRecord record = Execute(AbortingRequest());

            for (int i = 0; i < record.Units.Count; i++)
            {
                Assert.AreEqual(UnitClosureStatus.NormalCompletion, record.Units[i].Closure.Status);
                Assert.AreNotEqual(SafetyStatus.SafetyAborted, record.Units[i].Closure.SafetyStatus);
            }
        }

        [Test]
        public void EquivalentInputs_ReproduceTheSameSafetyEvidence()
        {
            ExecutionScheduler scheduler = new ExecutionScheduler(new ExecutionTraceBuilder());

            ExecutionRecord first = scheduler.Execute(AbortingRequest());
            ExecutionRecord second = scheduler.Execute(AbortingRequest());

            SafetyAbortPayload firstPayload =
                (SafetyAbortPayload)SingleSubtype(first, ExecutionEventSubtypes.SafetyLimitReached).Payload;
            SafetyAbortPayload secondPayload =
                (SafetyAbortPayload)SingleSubtype(second, ExecutionEventSubtypes.SafetyLimitReached).Payload;

            Assert.AreEqual(firstPayload.BreachedLimits[0].LimitName, secondPayload.BreachedLimits[0].LimitName);
            Assert.AreEqual(
                firstPayload.OverLimitOccurrenceIdentity,
                secondPayload.OverLimitOccurrenceIdentity);
            Assert.AreEqual(first.ResultValidity, second.ResultValidity);
            Assert.AreEqual(first.SafetyCounts, second.SafetyCounts);
        }

        [Test]
        public void EquivalentInputs_ReproduceStructurallyEqualRecords()
        {
            ExecutionScheduler scheduler = new ExecutionScheduler(new ExecutionTraceBuilder());

            ExecutionRecord first = scheduler.Execute(AbortingRequest());
            ExecutionRecord second = scheduler.Execute(AbortingRequest());

            Assert.AreEqual(first, second);
        }

        [Test]
        public void NonSafetyDefect_EscapesExecuteInsteadOfBeingCaughtAsAnAbort()
        {
            ExecutionScheduler scheduler = new ExecutionScheduler(new ExecutionTraceBuilder());
            ExecutionRequest request = SkippedConditionRequest(new List<DependencyInstance>
            {
                SchedulerFixtures.SafeModeInstance(70),
                SchedulerFixtures.SafeModeInstance(71)
            });

            InvalidOperationException thrown = Assert.Throws<InvalidOperationException>(
                () => scheduler.Execute(request));

            Assert.IsNotInstanceOf<SafetyAbortSignal>(thrown);
            StringAssert.Contains("rescue", thrown.Message);
        }

        /// <summary>
        /// A false Condition over one contained <c>Signal += 1</c> Instruction followed by a
        /// top-level <c>Signal += 1</c> Instruction. The predicate reads Value against a floor the
        /// zero initial state cannot meet, so the contained unit activates and is skipped while the
        /// top-level unit resolves — two activated units either way.
        /// </summary>
        /// <param name="installed">The installed Dependency instances.</param>
        /// <returns>The assembled request.</returns>
        private static ExecutionRequest SkippedConditionRequest(List<DependencyInstance> installed)
        {
            StructureInstance condition = SchedulerFixtures.ConditionStructure(
                20,
                CoreRegister.Value,
                PredicateComparison.AtLeast,
                3,
                2);
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForStructureHeader(new SourcePosition(1), condition),
                SourceSlot.ForContainedInstruction(
                    new SourcePosition(2),
                    condition,
                    SchedulerFixtures.SignalAddInstance(21, 1)),
                SourceSlot.ForInstruction(new SourcePosition(3), SchedulerFixtures.SignalAddInstance(22, 1))
            });

            return SchedulerFixtures.RequestOver(
                arrangement,
                SchedulerFixtures.ZeroState(),
                new InstanceIDSource(),
                installed);
        }

        /// <summary>
        /// A Repeat one iteration past the source-execution-unit ceiling — the cheapest safety abort
        /// in the suite, reused wherever a fixture only needs to be aborting.
        /// </summary>
        /// <returns>The assembled request.</returns>
        private static ExecutionRequest AbortingRequest()
        {
            StructureInstance structure = SchedulerFixtures.RepeatStructure(
                50,
                SafetyCeilings.SourceExecutionUnitsPerExecution + 1,
                2);
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForStructureHeader(new SourcePosition(1), structure),
                SourceSlot.ForContainedInstruction(
                    new SourcePosition(2),
                    structure,
                    SchedulerFixtures.SignalAddInstance(51, 1))
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
