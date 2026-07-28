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
    /// Tests the effect-reaction ceiling end to end through the scheduler's entry point —
    /// CAB-SAFE-156, all four clauses.
    /// The fixture is a flat fan-out rather than a chain. Five every-qualifying reaction Directives
    /// observe positive player Value gains; each resolves once per player unit and writes Value
    /// itself, but a reaction's own quantity change is not from a primary operation, and the
    /// operation-class qualifier admits only primary operations of player-owned units. So the
    /// cascade cannot re-qualify: recursion never exceeds one level and each unit contributes
    /// exactly five reactions. Five is the smallest instance count that reaches the reaction ceiling
    /// before the unit ceiling — with four, 16,384 reactions need exactly 4,096 units, so the unit
    /// ceiling arrives first. At five, reaction 16,385 is attempted during unit 3,277, comfortably
    /// inside the 4,096-unit budget.
    /// This is the suite that lands the one-open-unit abort SHA-02 deferred. Every breach site
    /// SHA-02 could reach fired with no unit open, because the scheduler closes a unit before
    /// draining its descendants. The reaction site is different: it breaches inside the operation
    /// path, with the unit still open, so abort finalization closes exactly that one unit with
    /// SafetyIntervention and the safety events carry its identity.
    /// The large fixture runs once in one-time setup. It is the most expensive in the EditMode
    /// suite — roughly 16,384 reactions, each offering to five effects that then fail the
    /// operation-class qualifier, so the record carries on the order of a hundred thousand events.
    /// Its assertions all read the one shared record; never re-run it per test.
    /// </summary>
    public sealed class ExecutionSchedulerReactionCeilingTests
    {
        /// <summary>
        /// The number of reaction Directive instances, and so the reactions each player unit
        /// contributes.
        /// </summary>
        private const int ReactionsPerUnit = 5;

        /// <summary>
        /// The unit during which reaction 16,385 is attempted: the ceiling divided by the per-unit
        /// contribution, rounded up.
        /// </summary>
        private const int BreachingUnit =
            (SafetyCeilings.EffectReactionsPerExecution / ReactionsPerUnit) + 1;

        /// <summary>
        /// The authored iteration count, set past the breaching unit with margin so the reaction
        /// ceiling is certainly reached while staying far below the unit ceiling.
        /// </summary>
        private const int FixtureIterations = BreachingUnit + 64;

        /// <summary>
        /// The record of the five-reaction fan-out that breaches the reaction ceiling.
        /// </summary>
        private ExecutionRecord _reactionRecord;

        /// <summary>
        /// Runs the shared fan-out once for the whole suite.
        /// </summary>
        [OneTimeSetUp]
        public void RunSharedFixture()
        {
            _reactionRecord = Execute(ReactionFanOutRequest(FixtureIterations, ReactionsPerUnit));
        }

        [Test]
        public void ReactionFanOut_Permits16384ResolvedReactions()
        {
            Assert.AreEqual(
                SafetyCeilings.EffectReactionsPerExecution,
                _reactionRecord.SafetyCounts.EffectReactions);
            Assert.AreEqual(
                SafetyCeilings.EffectReactionsPerExecution,
                CountSubtype(_reactionRecord, ExecutionEventSubtypes.ImmediateReactionResolved));
        }

        [Test]
        public void ReactionFanOut_Attempted16385_TriggersSafetyIntervention()
        {
            EventEvidence reached = SingleSubtype(_reactionRecord, ExecutionEventSubtypes.SafetyLimitReached);
            SafetyAbortPayload payload = (SafetyAbortPayload)reached.Payload;

            Assert.AreEqual(ExecutionCompletionStatus.SafetyAborted, _reactionRecord.CompletionStatus);
            Assert.AreEqual(SafetyStatus.SafetyAborted, _reactionRecord.SafetyStatus);
            Assert.AreEqual(
                SafetyAbortSignal.LimitName(SafetyLimitFlags.EffectReactions),
                payload.BreachedLimits[0].LimitName);
            Assert.AreEqual(SafetyCeilings.EffectReactionsPerExecution, payload.BreachedLimits[0].Ceiling);
            Assert.AreEqual(
                SafetyCeilings.EffectReactionsPerExecution,
                payload.BreachedLimits[0].CountAtBreach);
        }

        [Test]
        public void ReactionFanOut_BreachesOnlyTheReactionLimit()
        {
            EventEvidence reached = SingleSubtype(_reactionRecord, ExecutionEventSubtypes.SafetyLimitReached);
            SafetyAbortPayload payload = (SafetyAbortPayload)reached.Payload;

            Assert.AreEqual(1, payload.BreachedLimits.Count);
            Assert.Less(
                _reactionRecord.SafetyCounts.SourceExecutionUnits,
                SafetyCeilings.SourceExecutionUnitsPerExecution);
        }

        [Test]
        public void ReactionBreach_StopsAtTheArithmeticallyPredictedUnit()
        {
            Assert.AreEqual(BreachingUnit, _reactionRecord.SafetyCounts.SourceExecutionUnits);
            Assert.AreEqual(BreachingUnit, _reactionRecord.Units.Count);
        }

        [Test]
        public void ReactionBreach_HappensInsideAnOpenUnit()
        {
            EventEvidence reached = SingleSubtype(_reactionRecord, ExecutionEventSubtypes.SafetyLimitReached);
            SafetyAbortPayload payload = (SafetyAbortPayload)reached.Payload;

            Assert.IsNotNull(reached.ContainingUnit);
            Assert.AreEqual(reached.ContainingUnit.Value, payload.AffectedUnit);
        }

        [Test]
        public void ReactionBreach_ClosesTheSingleOpenUnitWithSafetyIntervention()
        {
            Assert.AreEqual(1, CountClosures(_reactionRecord, UnitClosureStatus.SafetyIntervention));
        }

        [Test]
        public void ReactionBreach_ClosesTheOpenUnitItNames()
        {
            EventEvidence reached = SingleSubtype(_reactionRecord, ExecutionEventSubtypes.SafetyLimitReached);
            RuntimeUnitRecord intervened = null;
            for (int i = 0; i < _reactionRecord.Units.Count; i++)
            {
                if (_reactionRecord.Units[i].Closure.Status == UnitClosureStatus.SafetyIntervention)
                    intervened = _reactionRecord.Units[i];
            }

            Assert.IsNotNull(intervened);
            Assert.AreEqual(reached.ContainingUnit.Value, intervened.Identity);
        }

        [Test]
        public void ReactionBreach_LeavesEveryEarlierUnitNormallyCompleted()
        {
            Assert.AreEqual(
                BreachingUnit - 1,
                CountClosures(_reactionRecord, UnitClosureStatus.NormalCompletion));
        }

        [Test]
        public void ReactionBreach_NeverProducesBranchTerminatedClosures()
        {
            Assert.AreEqual(0, CountClosures(_reactionRecord, UnitClosureStatus.BranchTerminated));
        }

        [Test]
        public void ReactionBreach_IdentityNamesTheReactionResolution()
        {
            EventEvidence reached = SingleSubtype(_reactionRecord, ExecutionEventSubtypes.SafetyLimitReached);
            SafetyAbortPayload payload = (SafetyAbortPayload)reached.Payload;

            StringAssert.StartsWith(
                SafetyAbortSignal.ComposeOccurrenceIdentity(
                    EventFamilies.Reaction,
                    ExecutionEventSubtypes.ImmediateReactionResolved,
                    string.Empty),
                payload.OverLimitOccurrenceIdentity);
        }

        [Test]
        public void ReactionBreach_ExecutionIsInvalidAndHandoffBlocked()
        {
            Assert.AreEqual(ResultValidityStatus.Invalid, _reactionRecord.ResultValidity);
            Assert.AreEqual(ResultReviewHandoffStatus.Blocked, _reactionRecord.HandoffStatus);
            Assert.AreEqual(0, CountSubtype(_reactionRecord, ExecutionEventSubtypes.ExecutionCompleted));
            Assert.AreEqual(
                ExecutionEventSubtypes.ExecutionSafetyAborted,
                _reactionRecord.Events[_reactionRecord.Events.Count - 1].Evidence.Subtype);
        }

        [Test]
        public void FailedQualification_DoesNotCountTowardTheReactionCeiling()
        {
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForInstruction(new SourcePosition(1), SchedulerFixtures.ValueAddInstance(50, 1))
            });
            ExecutionRecord record = Execute(SchedulerFixtures.RequestOver(
                arrangement,
                SchedulerFixtures.ZeroState(),
                new InstanceIDSource(),
                new List<DependencyInstance>(),
                new List<DirectiveInstance> { SchedulerFixtures.SignalGainReactionPragma(100, 1) }));

            Assert.AreEqual(ExecutionCompletionStatus.Completed, record.CompletionStatus);
            Assert.AreEqual(0, record.SafetyCounts.EffectReactions);
            Assert.AreEqual(0, CountSubtype(record, ExecutionEventSubtypes.ImmediateReactionResolved));
            Assert.AreEqual(1, CountSubtype(record, ExecutionEventSubtypes.EffectFailedToQualify));
        }

        [Test]
        public void CommittedZeroDeltaReaction_CountsTowardTheReactionCeiling()
        {
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForInstruction(new SourcePosition(1), SchedulerFixtures.ValueAddInstance(50, 1))
            });
            ExecutionRecord record = Execute(SchedulerFixtures.RequestOver(
                arrangement,
                SchedulerFixtures.ZeroState(),
                new InstanceIDSource(),
                new List<DependencyInstance>(),
                new List<DirectiveInstance> { SchedulerFixtures.ValueGainReactionPragma(100, 0) }));

            Assert.AreEqual(ExecutionCompletionStatus.Completed, record.CompletionStatus);
            Assert.AreEqual(1, record.SafetyCounts.EffectReactions);
            Assert.AreEqual(1, CountSubtype(record, ExecutionEventSubtypes.ImmediateReactionResolved));
            Assert.AreEqual(1, record.FinalState.FinalValue.Value);
        }

        [Test]
        public void ReactionBreach_IsDeterministicAcrossTwoRuns()
        {
            ExecutionScheduler scheduler = new ExecutionScheduler(new ExecutionTraceBuilder());
            ExecutionRequest request = ReactionFanOutRequest(BreachingUnit, ReactionsPerUnit);

            ExecutionRecord first = scheduler.Execute(request);
            ExecutionRecord second = scheduler.Execute(request);

            Assert.AreEqual(ExecutionCompletionStatus.SafetyAborted, first.CompletionStatus);
            Assert.AreEqual(first, second);
        }

        /// <summary>
        /// A Repeat over one contained <c>Value += 1</c> Instruction, iterating the given count,
        /// under the given number of distinct every-qualifying Value-gain reaction Directives. Each
        /// iteration opens one unit whose primary operation qualifies every reaction instance
        /// exactly once, so the reaction count advances by the instance count per unit.
        /// </summary>
        /// <param name="iterations">The authored iteration count.</param>
        /// <param name="reactionCount">The number of distinct reaction instances to install.</param>
        /// <returns>The assembled request.</returns>
        private static ExecutionRequest ReactionFanOutRequest(int iterations, int reactionCount)
        {
            StructureInstance structure = SchedulerFixtures.RepeatStructure(50, iterations, 2);
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForStructureHeader(new SourcePosition(1), structure),
                SourceSlot.ForContainedInstruction(
                    new SourcePosition(2),
                    structure,
                    SchedulerFixtures.ValueAddInstance(51, 1))
            });

            List<DirectiveInstance> reactions = new List<DirectiveInstance>();
            for (int i = 0; i < reactionCount; i++)
            {
                reactions.Add(SchedulerFixtures.ValueGainReactionPragma(100 + i, 1));
            }

            return SchedulerFixtures.RequestOver(
                arrangement,
                SchedulerFixtures.ZeroState(),
                new InstanceIDSource(),
                new List<DependencyInstance>(),
                reactions);
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
        /// Counts the units closed with one closure status.
        /// </summary>
        /// <param name="record">The frozen record.</param>
        /// <param name="status">The closure status to count.</param>
        /// <returns>The number of matching units.</returns>
        private static int CountClosures(ExecutionRecord record, UnitClosureStatus status)
        {
            int count = 0;
            for (int i = 0; i < record.Units.Count; i++)
            {
                if (record.Units[i].Closure.Status == status)
                    count++;
            }

            return count;
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
        /// The single event carrying one subtype, asserting exactly one exists.
        /// </summary>
        /// <param name="record">The frozen record.</param>
        /// <param name="subtype">The subtype to find.</param>
        /// <returns>The matching evidence.</returns>
        private static EventEvidence SingleSubtype(ExecutionRecord record, string subtype)
        {
            int found = -1;
            int count = 0;
            for (int i = 0; i < record.Events.Count; i++)
            {
                if (record.Events[i].Evidence.Subtype != subtype)
                    continue;

                if (found < 0)
                    found = i;

                count++;
            }

            Assert.AreEqual(1, count);
            return record.Events[found].Evidence;
        }
    }
}
