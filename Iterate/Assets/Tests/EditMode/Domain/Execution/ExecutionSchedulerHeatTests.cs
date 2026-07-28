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
    /// The multiplication half of the CAB-EVT-753 conformance matrix, driven end to end through the
    /// scheduler's entry point: the pre-check at Heat 0, 1, and 2; the multiplication that raises Heat
    /// to three resolving normally; the multiplication beginning at three being skipped; the unrescued
    /// skip gaining nothing; the SAFE MODE rescue with no second pre-check and a clamped gain; and the
    /// independence of Repeat occurrences, added executions, and immutable Core multiplications.
    /// Every fixture reaches Heat through real multiplications rather than a seeded counter, because
    /// the counter has no external write surface — the pre-check offer is its only producer. The
    /// registers are reset neutrally to zero in phase 3, so each arrangement opens by adding one to
    /// Value before any multiplication can do work.
    /// The two canonical examples this suite pins are the CAB's own: Heat throttle without rescue
    /// (line 10525) and with rescue (line 10534). The added-multiplication example (line 11829) is
    /// pinned here too, since its whole shape is a Core-warmed Heat 2 followed by a player
    /// multiplication whose OVERCLOCK descendant begins at three.
    /// </summary>
    public sealed class ExecutionSchedulerHeatTests
    {
        /// <summary>
        /// The Process-rule instance identity every fixture in this suite configures.
        /// </summary>
        private const int RuleInstance = 80;

        /// <summary>
        /// The record of the five-slot ladder that climbs Heat to three and then skips.
        /// </summary>
        private ExecutionRecord _ladder;

        /// <summary>
        /// The record of the same ladder with SAFE MODE installed, so the fifth slot is rescued.
        /// </summary>
        private ExecutionRecord _rescued;

        /// <summary>
        /// Runs the two shared ladders once for the whole suite.
        /// </summary>
        [OneTimeSetUp]
        public void RunSharedFixtures()
        {
            _ladder = HeatRecordReader.Execute(LadderRequest(false));
            _rescued = HeatRecordReader.Execute(LadderRequest(true));
        }

        [Test]
        public void MultiplicationBelowThree_ResolvesAndGainsOneEachTime()
        {
            IReadOnlyList<QuantityChangePayload> commits = HeatRecordReader.CounterCommits(_ladder);

            Assert.AreEqual(3, commits.Count, "Three multiplications began below three, so three gains commit.");
            HeatRecordReader.AssertGain(commits[0], 0, 1);
            HeatRecordReader.AssertGain(commits[1], 1, 2);
            HeatRecordReader.AssertGain(commits[2], 2, 3);
        }

        [Test]
        public void MultiplicationRaisingHeatToThree_ResolvesNormally()
        {
            EventEvidence finalized = HeatRecordReader.DispositionAt(_ladder, new SourcePosition(4));

            Assert.AreEqual(EventDisposition.Resolved, finalized.Disposition);
            Assert.AreEqual(3, HeatRecordReader.CounterCommits(_ladder)[2].FinalValue);
            Assert.AreEqual(8, _ladder.FinalState.FinalValue.Value, "0 → +1 → ×2 → ×2 → ×2 = 8.");
        }

        [Test]
        public void MultiplicationBeginningAtThree_IsSkippedByTheProcessRule()
        {
            EventEvidence intervention = HeatRecordReader.SingleSubtype(_ladder, ExecutionEventSubtypes.ProcessRuleIntervened);
            EventEvidence finalized = HeatRecordReader.DispositionAt(_ladder, new SourcePosition(5));

            Assert.AreEqual(EventFamilies.Intervention, intervention.Family);
            Assert.AreEqual(EventDisposition.Skipped, intervention.Disposition);
            Assert.AreEqual(EventDisposition.Skipped, finalized.Disposition);
            Assert.AreEqual(1, HeatRecordReader.CountSubtype(_ladder, ExecutionEventSubtypes.SourceExecutionSkipped));
        }

        [Test]
        public void InterventionEvidence_NamesRuleOriginAndPriorCounterState()
        {
            EventEvidence intervention = HeatRecordReader.SingleSubtype(_ladder, ExecutionEventSubtypes.ProcessRuleIntervened);
            QuantityChangePayload state = (QuantityChangePayload)intervention.Payload;

            Assert.AreEqual(new InstanceID(RuleInstance), intervention.EffectOriginInstance);
            Assert.AreEqual(HeatRecordReader.HeatIdentity, state.QuantityIdentity);
            Assert.AreEqual(QuantityCategory.ProcessCounter, state.Category);
            Assert.AreEqual(3, state.PriorValue, "The counter state before the intervention is the pre-check reading.");
            Assert.AreEqual(0, state.FinalDelta, "An intervention changes no counter; it only reports one.");
            Assert.AreEqual(new SourcePosition(5), intervention.Position);
        }

        [Test]
        public void UnrescuedHeatSkip_GainsNoHeatAndLeavesValueUnchanged()
        {
            Assert.AreEqual(3, HeatRecordReader.CounterCommits(_ladder).Count, "A skipped multiplication requests no gain.");
            Assert.AreEqual(3, HeatRecordReader.FinalHeat(_ladder));
            Assert.AreEqual(8, _ladder.FinalState.FinalValue.Value, "The skipped ×2 never doubled 8 to 16.");
        }

        [Test]
        public void SafeModeRescue_ResolvesTheMultiplicationWithoutASecondPreCheck()
        {
            EventEvidence finalized = HeatRecordReader.DispositionAt(_rescued, new SourcePosition(5));

            Assert.AreEqual(EventDisposition.Rescued, finalized.Disposition);
            Assert.AreEqual(16, _rescued.FinalState.FinalValue.Value, "The rescued ×2 doubled 8 to 16.");
            Assert.AreEqual(
                1,
                HeatRecordReader.CountSubtype(_rescued, ExecutionEventSubtypes.ProcessRuleIntervened),
                "The pre-check happened once, at the pending offer; the rescue resumes past it.");
        }

        [Test]
        public void RescuedMultiplication_RequestsTheOrdinaryGainClampedAtThree()
        {
            IReadOnlyList<QuantityChangePayload> commits = HeatRecordReader.CounterCommits(_rescued);
            QuantityChangePayload clamped = commits[commits.Count - 1];

            Assert.AreEqual(4, commits.Count, "Three ordinary gains plus the rescued request.");
            Assert.AreEqual(1, clamped.RequestedAmount);
            Assert.AreEqual(0, clamped.FinalDelta);
            Assert.AreEqual(3, clamped.PriorValue);
            Assert.AreEqual(3, clamped.FinalValue);
            Assert.IsNotNull(clamped.AppliedBounds, "The maximum bound applied, and that is the clamp evidence.");
            Assert.AreEqual(3, clamped.AppliedBounds.Maximum);
        }

        [Test]
        public void RescuedHeatOperation_RetainsItsSkipAsTheOriginalDisposition()
        {
            RuntimeUnitRecord unit = HeatRecordReader.UnitAt(_rescued, new SourcePosition(5));

            Assert.AreEqual(EventDisposition.Rescued, unit.Closure.FinalDisposition);
            Assert.AreEqual(EventDisposition.Skipped, unit.Closure.OriginalDisposition);
            Assert.AreEqual(1, HeatRecordReader.CountSubtype(_rescued, ExecutionEventSubtypes.SourceExecutionRescued));
        }

        [Test]
        public void RepeatOccurrences_PreCheckIndependently()
        {
            ExecutionRecord record = HeatRecordReader.Execute(RepeatRequest(4));
            IReadOnlyList<QuantityChangePayload> commits = HeatRecordReader.CounterCommits(record);

            Assert.AreEqual(3, commits.Count, "Iterations one to three gained; the fourth began at three.");
            Assert.AreEqual(8, record.FinalState.FinalValue.Value);
            Assert.AreEqual(1, HeatRecordReader.CountSubtype(record, ExecutionEventSubtypes.ProcessRuleIntervened));
            Assert.AreEqual(3, HeatRecordReader.FinalHeat(record));
        }

        [Test]
        public void ImmutableCoreMultiplication_PerformsTheSamePreCheckAndGain()
        {
            ExecutionRecord record = HeatRecordReader.Execute(CoreOnlyRequest());
            IReadOnlyList<QuantityChangePayload> commits = HeatRecordReader.CounterCommits(record);

            Assert.AreEqual(3, commits.Count, "Core ownership exempts nothing.");
            Assert.AreEqual(1, HeatRecordReader.CountSubtype(record, ExecutionEventSubtypes.ProcessRuleIntervened));
            Assert.AreEqual(8, record.FinalState.FinalValue.Value);
        }

        [Test]
        public void AddedMultiplicationUnderHeat_SkipsTheDescendantAndClampsItsRescuedGain()
        {
            ExecutionRecord record = HeatRecordReader.Execute(AddedMultiplicationRequest());
            IReadOnlyList<QuantityChangePayload> commits = HeatRecordReader.CounterCommits(record);

            Assert.AreEqual(1, HeatRecordReader.CountSubtype(record, ExecutionEventSubtypes.AddedExecutionStarted));
            Assert.AreEqual(1, HeatRecordReader.CountSubtype(record, ExecutionEventSubtypes.ProcessRuleIntervened));
            Assert.AreEqual(1, HeatRecordReader.CountSubtype(record, ExecutionEventSubtypes.SourceExecutionRescued));
            Assert.AreEqual(4, commits.Count, "Two Core gains, the player gain, then the descendant's clamped request.");
            HeatRecordReader.AssertGain(commits[2], 2, 3);
            Assert.AreEqual(0, commits[3].FinalDelta);
            Assert.AreEqual(3, commits[3].FinalValue);
            Assert.AreEqual(16, record.FinalState.FinalValue.Value, "1 → 2 → 4 → 8 → rescued ×2 = 16.");
        }

        [Test]
        public void HeatThresholdEntry_IsRecordedOnceAfterTheGainFinalizes()
        {
            IReadOnlyList<EventEvidence> crossings = HeatRecordReader.HeatCrossings(_ladder);

            Assert.AreEqual(1, crossings.Count);
            Assert.AreEqual(ExecutionEventSubtypes.ThresholdCrossedUpward, crossings[0].Subtype);
            Assert.AreEqual(HeatRecordReader.ThrottlingBand, crossings[0].TargetIdentity);
        }

        /// <summary>
        /// A ladder of one <c>Value += 1</c> followed by four <c>Value ×= 2</c> player Instructions,
        /// under THERMAL THROTTLE. The three multiplications at Heat 0, 1, and 2 resolve and gain; the
        /// fourth begins at three. SAFE MODE is installed only when a rescue is wanted.
        /// </summary>
        /// <param name="withSafeMode">Whether to install the rescuing Dependency.</param>
        /// <returns>The assembled request.</returns>
        private static ExecutionRequest LadderRequest(bool withSafeMode)
        {
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForInstruction(new SourcePosition(1), SchedulerFixtures.ValueAddInstance(50, 1)),
                SourceSlot.ForInstruction(new SourcePosition(2), SchedulerFixtures.ValueMultiplyInstance(51, 2)),
                SourceSlot.ForInstruction(new SourcePosition(3), SchedulerFixtures.ValueMultiplyInstance(52, 2)),
                SourceSlot.ForInstruction(new SourcePosition(4), SchedulerFixtures.ValueMultiplyInstance(53, 2)),
                SourceSlot.ForInstruction(new SourcePosition(5), SchedulerFixtures.ValueMultiplyInstance(54, 2))
            });

            List<DependencyInstance> installed = new List<DependencyInstance>();
            if (withSafeMode)
                installed.Add(SchedulerFixtures.SafeModeInstance(60));

            return RequestOverArrangement(arrangement, null, installed, new List<DirectiveInstance>());
        }

        /// <summary>
        /// A Core <c>Value += 1</c> followed by a Repeat over one contained <c>Value ×= 2</c>
        /// Instruction, iterating the given count under THERMAL THROTTLE.
        /// </summary>
        /// <param name="iterations">The authored iteration count.</param>
        /// <returns>The assembled request.</returns>
        private static ExecutionRequest RepeatRequest(int iterations)
        {
            StructureInstance structure = SchedulerFixtures.RepeatStructure(55, iterations, 2);
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForCore(new SourcePosition(1), SchedulerFixtures.ValueCoreLine("L01", CoreLineOperator.Add, 1)),
                SourceSlot.ForStructureHeader(new SourcePosition(2), structure),
                SourceSlot.ForContainedInstruction(
                    new SourcePosition(3),
                    structure,
                    SchedulerFixtures.ValueMultiplyInstance(56, 2))
            });

            return RequestOverArrangement(arrangement, null, new List<DependencyInstance>(), new List<DirectiveInstance>());
        }

        /// <summary>
        /// A Core-only ladder: <c>Value += 1</c> then four Core <c>Value ×= 2</c> lines, so every
        /// multiplication is immutable-Core-owned.
        /// </summary>
        /// <returns>The assembled request.</returns>
        private static ExecutionRequest CoreOnlyRequest()
        {
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForCore(new SourcePosition(1), SchedulerFixtures.ValueCoreLine("L01", CoreLineOperator.Add, 1)),
                SourceSlot.ForCore(new SourcePosition(2), SchedulerFixtures.ValueCoreLine("L02", CoreLineOperator.Multiply, 2)),
                SourceSlot.ForCore(new SourcePosition(3), SchedulerFixtures.ValueCoreLine("L03", CoreLineOperator.Multiply, 2)),
                SourceSlot.ForCore(new SourcePosition(4), SchedulerFixtures.ValueCoreLine("L04", CoreLineOperator.Multiply, 2)),
                SourceSlot.ForCore(new SourcePosition(5), SchedulerFixtures.ValueCoreLine("L05", CoreLineOperator.Multiply, 2))
            });

            return RequestOverArrangement(arrangement, null, new List<DependencyInstance>(), new List<DirectiveInstance>());
        }

        /// <summary>
        /// The CAB's added-multiplication example: two Core multiplications warm Heat to two, then a
        /// player multiplication resolves at two and raises Heat to three while OVERCLOCK requests an
        /// added execution of it. The descendant begins at three, is skipped, and SAFE MODE rescues it.
        /// The Core lines carry the warm-up so the once-per-execution creator is still unconsumed when
        /// the player multiplication resolves.
        /// </summary>
        /// <returns>The assembled request.</returns>
        private static ExecutionRequest AddedMultiplicationRequest()
        {
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForCore(new SourcePosition(1), SchedulerFixtures.ValueCoreLine("L01", CoreLineOperator.Add, 1)),
                SourceSlot.ForCore(new SourcePosition(2), SchedulerFixtures.ValueCoreLine("L02", CoreLineOperator.Multiply, 2)),
                SourceSlot.ForCore(new SourcePosition(3), SchedulerFixtures.ValueCoreLine("L03", CoreLineOperator.Multiply, 2)),
                SourceSlot.ForInstruction(new SourcePosition(4), SchedulerFixtures.ValueMultiplyInstance(57, 2))
            });

            return RequestOverArrangement(
                arrangement,
                null,
                new List<DependencyInstance> { SchedulerFixtures.SafeModeInstance(60) },
                new List<DirectiveInstance> { SchedulerFixtures.OverclockPragma(70) });
        }

        /// <summary>
        /// Assembles a request over the arrangement under THERMAL THROTTLE with the given designated
        /// final Core output, installed Dependencies, and pragmas.
        /// </summary>
        /// <param name="arrangement">The source arrangement.</param>
        /// <param name="designatedOutput">The designated final Core output position, or null.</param>
        /// <param name="installed">The installed Dependency instances.</param>
        /// <param name="pragmas">The active Directive pragmas.</param>
        /// <returns>The assembled request.</returns>
        private static ExecutionRequest RequestOverArrangement(
            SourceArrangement arrangement,
            SourcePosition? designatedOutput,
            List<DependencyInstance> installed,
            List<DirectiveInstance> pragmas)
        {
            return SchedulerFixtures.RequestOver(
                arrangement,
                SchedulerFixtures.ZeroState(),
                SchedulerFixtures.ConfigurationWithRule(SchedulerFixtures.ThermalThrottleRule(RuleInstance), designatedOutput),
                installed,
                pragmas);
        }
    }
}
