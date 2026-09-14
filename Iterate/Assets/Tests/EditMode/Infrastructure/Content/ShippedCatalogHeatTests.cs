using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using NUnit.Framework;
using Iterate.Application.Content;
using Iterate.Application.Content.Json;
using Iterate.Domain.Compilation;
using Iterate.Domain.Content;
using Iterate.Domain.Execution;
using Iterate.Domain.Trace;
using Iterate.Domain.Values;
using Iterate.Infrastructure.Content;

namespace Iterate.Infrastructure.Content.Tests
{
    /// <summary>
    /// The child's Heat conformance evidence over the real frozen catalog, driven on the Design
    /// §28.9 Core exactly as that section prints it — <c>Value = 1</c>, <c>Signal = 0</c>, six player
    /// slots, then <c>Value ×= 2</c>, <c>Score += Value</c>, <c>Value ×= 2</c>, <c>Score += Value</c>,
    /// with line 12 designated as the final Core output. Every register outcome is hand-traced in the
    /// Arrange comment of the test that asserts it.
    /// Nothing here is hand-authored content: the Process rule is `WB-PRC-001` out of the frozen
    /// catalog, the multiplications are `WB-INS-005`, the scoring Instruction is `WB-INS-012`, the
    /// rescuer is `WB-DEP-007 SAFE MODE`, and the separate scoring reaction is `WB-DEP-005 OUTPUT
    /// CACHE`. The Score bands are Balance §6.4's own 64 / 112 / 192, which no fixture here reaches —
    /// so every THRESHOLD-family event in these records is a Heat crossing, and the suite can assert
    /// on the family directly.
    /// The no-cap margin case (CAB-SAFE-161) closes the suite: the densest legal composition current
    /// content can express finishes with every safety count far below its ceiling, no SAFETY-family
    /// event of any kind, and Normal status on every event.
    /// </summary>
    public sealed class ShippedCatalogHeatTests
    {
        /// <summary>
        /// The one-based position of the designated final Core output in the Design §28.9 Core.
        /// </summary>
        private const int DesignatedFinalOutput = 12;

        /// <summary>
        /// The instance identity the shipped Process rule is configured under.
        /// </summary>
        private const int RuleInstance = 900;

        private ContentCatalog _catalog;

        [OneTimeSetUp]
        public void LoadCatalog()
        {
            string root = Path.Combine(UnityEngine.Application.dataPath, "StreamingAssets", "Catalog");
            CatalogDirectorySource source = new(root);
            CatalogLoader loader = new(new CatalogJsonReader(), new CatalogValidator(), new CatalogFreezer(), source);
            _catalog = loader.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();
        }

        [Test]
        public void ThrottledRun_SkipsEveryMultiplicationOnceHeatReachesThree()
        {
            // Six WB-INS-005 (Value ×= 2) in the player slots, no rescuer.
            // 01 Value 1 · 02 Signal 0 · 03 ×2 → 2, Heat 1 · 04 ×2 → 4, Heat 2 · 05 ×2 → 8, Heat 3
            // 06/07/08 skipped at Heat 3 · 09 Core ×2 skipped · 10 Score += 8 → 8, cools to Heat 2
            // 11 Core ×2 → 16, Heat 3 · 12 Score += 16 → 24, window closed, no cooling.
            ExecutionRecord record = Execute(CoreRequest(MultiplySlots(6), NoDependencies()));

            Assert.AreEqual(new ValueAmount(16), record.FinalState.FinalValue);
            Assert.AreEqual(new ScoreValue(24), record.FinalState.FinalScore);
            Assert.AreEqual(3, FinalHeat(record));
            Assert.AreEqual(
                4,
                CountSubtype(record, ExecutionEventSubtypes.ProcessRuleIntervened),
                "Player lines 6, 7, 8 and Core line 9 all began at three.");
            Assert.AreEqual(0, CountSubtype(record, ExecutionEventSubtypes.SourceExecutionRescued));
        }

        [Test]
        public void ThrottledRun_RecordsThreeGainsOneCoolingAndOneFurtherGain()
        {
            ExecutionRecord record = Execute(CoreRequest(MultiplySlots(6), NoDependencies()));
            IReadOnlyList<QuantityChangePayload> commits = CounterCommits(record);

            Assert.AreEqual(5, commits.Count);
            AssertCounterChange(commits[0], 0, 1, 1);
            AssertCounterChange(commits[1], 1, 1, 2);
            AssertCounterChange(commits[2], 2, 1, 3);
            AssertCounterChange(commits[3], 3, -1, 2);
            AssertCounterChange(commits[4], 2, 1, 3);
        }

        [Test]
        public void ThrottledRun_EntersLeavesAndReEntersThrottling()
        {
            ExecutionRecord record = Execute(CoreRequest(MultiplySlots(6), NoDependencies()));
            IReadOnlyList<EventEvidence> crossings = ThresholdEvents(record);

            Assert.AreEqual(3, crossings.Count, "Line 5 enters, line 10's cooling exits, line 11 re-enters.");
            Assert.AreEqual(ExecutionEventSubtypes.ThresholdCrossedUpward, crossings[0].Subtype);
            Assert.AreEqual(ExecutionEventSubtypes.ThresholdCrossedDownward, crossings[1].Subtype);
            Assert.AreEqual(ExecutionEventSubtypes.ThresholdCrossedUpward, crossings[2].Subtype);
        }

        [Test]
        public void RescuedRun_ResolvesTheFirstThrottledLineAndClampsItsGain()
        {
            // The same six multiplications with WB-DEP-007 SAFE MODE installed.
            // 03/04/05 gain to Heat 3 and Value 8 · 06 skipped then RESCUED → Value 16, gain clamps at 3
            // 07/08/09 skipped, the rescuer being once per execution · 10 Score += 16 → 16, cools to 2
            // 11 Core ×2 → 32, Heat 3 · 12 Score += 32 → 48, no cooling.
            ExecutionRecord record = Execute(CoreRequest(
                MultiplySlots(6),
                new List<DependencyInstance> { Dependency("WB-DEP-007", 800) }));
            IReadOnlyList<QuantityChangePayload> commits = CounterCommits(record);

            Assert.AreEqual(new ValueAmount(32), record.FinalState.FinalValue);
            Assert.AreEqual(new ScoreValue(48), record.FinalState.FinalScore);
            Assert.AreEqual(1, CountSubtype(record, ExecutionEventSubtypes.SourceExecutionRescued));
            Assert.AreEqual(4, CountSubtype(record, ExecutionEventSubtypes.ProcessRuleIntervened));
            Assert.AreEqual(6, commits.Count, "Three gains, the rescued clamp, one cooling, one further gain.");
            AssertCounterChange(commits[3], 3, 1, 3);
            Assert.IsNotNull(commits[3].AppliedBounds, "The rescued gain was reduced by the maximum bound.");
            Assert.AreEqual(3, commits[3].AppliedBounds.Maximum);
        }

        [Test]
        public void RescuedRun_RetainsTheSkipOnTheRescuedUnit()
        {
            ExecutionRecord record = Execute(CoreRequest(
                MultiplySlots(6),
                new List<DependencyInstance> { Dependency("WB-DEP-007", 800) }));
            RuntimeUnitRecord rescued = UnitAt(record, new SourcePosition(6));

            Assert.AreEqual(EventDisposition.Rescued, rescued.Closure.FinalDisposition);
            Assert.AreEqual(EventDisposition.Skipped, rescued.Closure.OriginalDisposition);
        }

        [Test]
        public void CooledRun_LetsLaterMultiplicationsResolveAndCoolsOutputCacheSeparately()
        {
            // Two WB-INS-005 then WB-INS-012 (Score += Value), with WB-DEP-005 OUTPUT CACHE installed.
            // 01 Value 1 · 03 ×2 → 2, Heat 1 · 04 ×2 → 4, Heat 2
            // 05 Score += 4 → 4; OUTPUT CACHE adds 2 → 6, and each positive Score change cools once:
            //    Heat 2 → 1 → 0 · 09 Core ×2 → 8, Heat 1 · 10 Score += 8 → 14, cools to Heat 0
            // 11 Core ×2 → 16, Heat 1 · 12 Score += 16 → 30, window closed, no cooling.
            List<SourceSlot> slots = new List<SourceSlot>
            {
                SourceSlot.ForInstruction(new SourcePosition(3), Instruction("WB-INS-005", 10)),
                SourceSlot.ForInstruction(new SourcePosition(4), Instruction("WB-INS-005", 11)),
                SourceSlot.ForInstruction(new SourcePosition(5), Instruction("WB-INS-012", 12))
            };

            ExecutionRecord record = Execute(CoreRequest(
                slots,
                new List<DependencyInstance> { Dependency("WB-DEP-005", 801) }));
            IReadOnlyList<QuantityChangePayload> commits = CounterCommits(record);

            Assert.AreEqual(new ValueAmount(16), record.FinalState.FinalValue);
            Assert.AreEqual(new ScoreValue(30), record.FinalState.FinalScore);
            Assert.AreEqual(0, CountSubtype(record, ExecutionEventSubtypes.ProcessRuleIntervened));
            Assert.AreEqual(7, commits.Count, "Two gains, two separate coolings, a gain, a cooling, a gain.");
            AssertCounterChange(commits[2], 2, -1, 1);
            AssertCounterChange(commits[3], 1, -1, 0);
            Assert.AreEqual(1, FinalHeat(record));
        }

        [Test]
        public void CooledRun_NeverEntersThrottling()
        {
            List<SourceSlot> slots = new List<SourceSlot>
            {
                SourceSlot.ForInstruction(new SourcePosition(3), Instruction("WB-INS-005", 10)),
                SourceSlot.ForInstruction(new SourcePosition(4), Instruction("WB-INS-005", 11)),
                SourceSlot.ForInstruction(new SourcePosition(5), Instruction("WB-INS-012", 12))
            };

            ExecutionRecord record = Execute(CoreRequest(
                slots,
                new List<DependencyInstance> { Dependency("WB-DEP-005", 801) }));

            Assert.AreEqual(
                0,
                ThresholdEvents(record).Count,
                "Cooling kept Heat below three throughout, so no crossing was recorded.");
        }

        [Test]
        public void LineTenCools_AndLineTwelveDoesNot()
        {
            ExecutionRecord record = Execute(CoreRequest(MultiplySlots(6), NoDependencies()));
            IReadOnlyList<QuantityChangePayload> commits = CounterCommits(record);

            Assert.AreEqual(-1, commits[3].FinalDelta, "Line 10 is an intermediate output and cooled.");
            Assert.AreEqual(
                1,
                commits[4].FinalDelta,
                "The last counter change is line 11's gain; line 12 produced none at all.");
            Assert.AreEqual(3, FinalHeat(record), "Line 12 scored 16 without cooling.");
        }

        [Test]
        public void HeatNeverExceedsTheBalanceMaximumOfThree()
        {
            ExecutionRecord throttled = Execute(CoreRequest(MultiplySlots(6), NoDependencies()));
            ExecutionRecord rescued = Execute(CoreRequest(
                MultiplySlots(6),
                new List<DependencyInstance> { Dependency("WB-DEP-007", 800) }));

            AssertHeatWithinBounds(throttled);
            AssertHeatWithinBounds(rescued);
        }

        [Test]
        public void DesignSectionCore_UsesTheBalanceThresholdsUnreachedByStarterContent()
        {
            ExecutionRecord record = Execute(CoreRequest(MultiplySlots(6), NoDependencies()));

            Assert.AreEqual(new ScoreValue(24), record.FinalState.FinalScore);
            Assert.AreEqual(
                0,
                CountFamily(record, EventFamilies.Threshold) - ThresholdEvents(record).Count,
                "No Score band was crossed, so every threshold event in the record is a Heat crossing.");
        }

        [Test]
        public void MaximumRecurrenceComposition_StaysFarBelowEverySafetyCeiling()
        {
            ExecutionRecord record = Execute(DensestCompositionRequest());

            Assert.AreEqual(ExecutionCompletionStatus.Completed, record.CompletionStatus);
            Assert.AreEqual(ResultValidityStatus.Valid, record.ResultValidity);
            Assert.Less(record.SafetyCounts.SourceExecutionUnits, SafetyCeilings.SourceExecutionUnitsPerExecution);
            Assert.Less(record.SafetyCounts.EffectReactions, SafetyCeilings.EffectReactionsPerExecution);
            Assert.Less(record.SafetyCounts.AddedDescendants, SafetyCeilings.AddedExecutionsPerActivation);
            Assert.Less(record.SafetyCounts.LineageDepthHighWater, SafetyCeilings.AddedExecutionLineageDepth);
            Assert.Less(record.SafetyCounts.OperationTransformations, SafetyCeilings.TransformationsPerPendingOperation);
        }

        [Test]
        public void MaximumRecurrenceComposition_EmitsNoSafetyEventAndStaysNormalThroughout()
        {
            ExecutionRecord record = Execute(DensestCompositionRequest());

            Assert.AreEqual(0, CountFamily(record, EventFamilies.Safety));
            Assert.AreEqual(0, CountSubtype(record, ExecutionEventSubtypes.SafetyLimitApproached));
            Assert.AreEqual(0, CountSubtype(record, ExecutionEventSubtypes.SafetyLimitReached));
            Assert.AreEqual(0, CountSubtype(record, ExecutionEventSubtypes.ExecutionSafetyAborted));
            Assert.AreEqual(1, CountSubtype(record, ExecutionEventSubtypes.ExecutionCompleted));
            Assert.AreEqual(SafetyStatus.Normal, record.SafetyStatus);

            for (int i = 0; i < record.Events.Count; i++)
            {
                Assert.AreEqual(SafetyStatus.Normal, record.Events[i].Evidence.SafetyStatus);
            }
        }

        [Test]
        public void MaximumRecurrenceComposition_IsDeterministicAcrossTwoRuns()
        {
            ExecutionScheduler scheduler = new ExecutionScheduler(new ExecutionTraceBuilder());
            ExecutionRequest request = DensestCompositionRequest();

            ExecutionRecord first = scheduler.Execute(request);
            ExecutionRecord second = scheduler.Execute(request);

            Assert.AreEqual(first, second);
        }

        /// <summary>
        /// The densest legal composition current content can express under the Heat pair: a Repeat 3
        /// over a shipped multiplication, a further multiplication, a scoring Instruction, and a
        /// Condition over a multiplication, with the rescuer, both scoring reactions, the selected-host
        /// modifier, and both creators installed.
        /// </summary>
        /// <returns>The assembled request.</returns>
        private ExecutionRequest DensestCompositionRequest()
        {
            StructureInstance repeat = Structure("WB-STR-006", 20);
            StructureInstance condition = Structure("WB-STR-002", 21);
            List<SourceSlot> slots = new List<SourceSlot>
            {
                SourceSlot.ForStructureHeader(new SourcePosition(3), repeat),
                SourceSlot.ForContainedInstruction(new SourcePosition(4), repeat, Instruction("WB-INS-005", 30)),
                SourceSlot.ForInstruction(new SourcePosition(5), Instruction("WB-INS-012", 31)),
                SourceSlot.ForStructureHeader(new SourcePosition(6), condition),
                SourceSlot.ForContainedInstruction(new SourcePosition(7), condition, Instruction("WB-INS-005", 32)),
                SourceSlot.ForInstruction(new SourcePosition(8), Instruction("WB-INS-012", 33))
            };

            return CoreRequest(slots, new List<DependencyInstance>
            {
                Dependency("WB-DEP-001", 810),
                Dependency("WB-DEP-005", 811),
                Dependency("WB-DEP-007", 812),
                Dependency("WB-DEP-009", 813),
                Dependency("WB-DEP-010", 814)
            });
        }

        /// <summary>
        /// The given number of shipped <c>Value ×= 2</c> Instructions, filling the player slots from
        /// position three.
        /// </summary>
        /// <param name="count">How many player slots to fill.</param>
        /// <returns>The player slots.</returns>
        private List<SourceSlot> MultiplySlots(int count)
        {
            List<SourceSlot> slots = new List<SourceSlot>(count);
            for (int i = 0; i < count; i++)
            {
                slots.Add(SourceSlot.ForInstruction(new SourcePosition(3 + i), Instruction("WB-INS-005", 10 + i)));
            }

            return slots;
        }

        /// <summary>
        /// Assembles a request over the Design §28.9 Core with the given player slots and installed
        /// Dependencies, under the shipped Process rule with line twelve designated as the final Core
        /// output. Player positions the caller leaves unfilled become empty slots.
        /// </summary>
        /// <param name="playerSlots">The occupied player slots, in ascending position order.</param>
        /// <param name="installed">The installed Dependency instances.</param>
        /// <returns>The assembled request.</returns>
        private ExecutionRequest CoreRequest(List<SourceSlot> playerSlots, List<DependencyInstance> installed)
        {
            List<SourceSlot> slots = new List<SourceSlot>
            {
                SourceSlot.ForCore(new SourcePosition(1), AssignConstant("core-01", CoreRegister.Value, 1)),
                SourceSlot.ForCore(new SourcePosition(2), AssignConstant("core-02", CoreRegister.Signal, 0))
            };

            int next = 3;
            for (int i = 0; i < playerSlots.Count; i++)
            {
                slots.Add(playerSlots[i]);
                next = playerSlots[i].Position.LineNumber + 1;
            }

            while (next <= 8)
            {
                slots.Add(SourceSlot.ForEmpty(new SourcePosition(next)));
                next++;
            }

            slots.Add(SourceSlot.ForCore(new SourcePosition(9), MultiplyConstant("core-09", CoreRegister.Value, 2)));
            slots.Add(SourceSlot.ForCore(new SourcePosition(10), AddRegister("core-10", CoreRegister.Score, CoreRegister.Value)));
            slots.Add(SourceSlot.ForCore(new SourcePosition(11), MultiplyConstant("core-11", CoreRegister.Value, 2)));
            slots.Add(SourceSlot.ForCore(new SourcePosition(DesignatedFinalOutput), AddRegister("core-12", CoreRegister.Score, CoreRegister.Value)));

            CompiledSource source = new CompiledSource(
                new SourceArrangement(slots),
                new List<DirectiveInstance>(),
                new CompilationCostBreakdown(CompilationClassification.Initial, 0, true, 0, new List<CostModifierEntry>(), 0, false));

            return new ExecutionRequest(source, ThrottleConfiguration(), StandardStamps(), ZeroState(), installed);
        }

        /// <summary>
        /// The Process configuration carrying the shipped Process rule, Balance §6.4's Score bands,
        /// and line twelve as the designated final Core output.
        /// </summary>
        /// <returns>The configuration.</returns>
        private ProcessExecutionConfiguration ThrottleConfiguration()
        {
            Assert.IsTrue(
                _catalog.TryGetProcessRule(new ProcessRuleID("WB-PRC-001"), out ProcessRuleDefinition definition),
                "WB-PRC-001");

            return new ProcessExecutionConfiguration(
                "exec",
                "compilation",
                "source-rev",
                "process",
                "core",
                "rule-config",
                "session-seed",
                new ProcessThresholds(new ScoreValue(64), new ScoreValue(112), new ScoreValue(192)),
                new ProcessRuleInstance(new InstanceID(RuleInstance), definition),
                new SourcePosition(DesignatedFinalOutput));
        }

        /// <summary>
        /// Wraps a real frozen Instruction definition in an unpatched instance.
        /// </summary>
        /// <param name="id">The Instruction's surrogate-key identity.</param>
        /// <param name="instance">The instance identity value.</param>
        /// <returns>The Instruction instance.</returns>
        private InstructionInstance Instruction(string id, int instance)
        {
            Assert.IsTrue(_catalog.TryGetInstruction(new InstructionID(id), out InstructionDefinition definition), id);

            return new InstructionInstance(new InstanceID(instance), definition, Array.Empty<PatchAttachment>());
        }

        /// <summary>
        /// Wraps a real frozen Structure definition in an instance.
        /// </summary>
        /// <param name="id">The Structure's surrogate-key identity.</param>
        /// <param name="instance">The instance identity value.</param>
        /// <returns>The Structure instance.</returns>
        private StructureInstance Structure(string id, int instance)
        {
            Assert.IsTrue(_catalog.TryGetStructure(new StructureID(id), out StructureDefinition definition), id);

            return new StructureInstance(new InstanceID(instance), definition);
        }

        /// <summary>
        /// Wraps a real frozen Dependency definition in an instance.
        /// </summary>
        /// <param name="id">The Dependency's surrogate-key identity.</param>
        /// <param name="instance">The instance identity value.</param>
        /// <returns>The Dependency instance.</returns>
        private DependencyInstance Dependency(string id, int instance)
        {
            Assert.IsTrue(_catalog.TryGetDependency(new DependencyID(id), out DependencyDefinition definition), id);

            return new DependencyInstance(new InstanceID(instance), definition);
        }

        /// <summary>
        /// Asserts that every counter change in the record stayed within the authored zero-to-three
        /// bounds, prior and final alike.
        /// </summary>
        /// <param name="record">The frozen record.</param>
        private static void AssertHeatWithinBounds(ExecutionRecord record)
        {
            IReadOnlyList<QuantityChangePayload> commits = CounterCommits(record);
            for (int i = 0; i < commits.Count; i++)
            {
                Assert.GreaterOrEqual(commits[i].FinalValue, 0);
                Assert.LessOrEqual(commits[i].FinalValue, 3, "Balance §6.4 fixes the Heat maximum at three.");
                Assert.GreaterOrEqual(commits[i].PriorValue, 0);
                Assert.LessOrEqual(commits[i].PriorValue, 3);
            }
        }

        /// <summary>
        /// Asserts one counter change's prior value, requested delta, and final value.
        /// </summary>
        /// <param name="commit">The counter-change payload.</param>
        /// <param name="prior">The expected prior value.</param>
        /// <param name="requested">The expected requested delta.</param>
        /// <param name="final">The expected final value.</param>
        private static void AssertCounterChange(
            QuantityChangePayload commit,
            int prior,
            int requested,
            int final
        )
        {
            Assert.AreEqual("HEAT", commit.QuantityIdentity);
            Assert.AreEqual(QuantityCategory.ProcessCounter, commit.Category);
            Assert.AreEqual(prior, commit.PriorValue);
            Assert.AreEqual(requested, commit.RequestedAmount);
            Assert.AreEqual(final, commit.FinalValue);
        }

        /// <summary>
        /// Collects the committed Process-counter changes in emission order.
        /// </summary>
        /// <param name="record">The frozen record.</param>
        /// <returns>The counter-change payloads.</returns>
        private static IReadOnlyList<QuantityChangePayload> CounterCommits(ExecutionRecord record)
        {
            List<QuantityChangePayload> commits = new List<QuantityChangePayload>();
            for (int i = 0; i < record.Events.Count; i++)
            {
                EventEvidence evidence = record.Events[i].Evidence;
                if (evidence.Subtype != ExecutionEventSubtypes.QuantityChanged)
                    continue;

                if (evidence.Payload is QuantityChangePayload { Category: QuantityCategory.ProcessCounter } payload)
                    commits.Add(payload);
            }

            return commits;
        }

        /// <summary>
        /// Reads the final counter value from the last committed change, or zero when none committed.
        /// </summary>
        /// <param name="record">The frozen record.</param>
        /// <returns>The final Heat value.</returns>
        private static int FinalHeat(ExecutionRecord record)
        {
            IReadOnlyList<QuantityChangePayload> commits = CounterCommits(record);

            return commits.Count == 0 ? 0 : commits[commits.Count - 1].FinalValue;
        }

        /// <summary>
        /// Collects the threshold-family events, every one of which is a Heat crossing at these bands.
        /// </summary>
        /// <param name="record">The frozen record.</param>
        /// <returns>The crossing events in emission order.</returns>
        private static IReadOnlyList<EventEvidence> ThresholdEvents(ExecutionRecord record)
        {
            List<EventEvidence> crossings = new List<EventEvidence>();
            for (int i = 0; i < record.Events.Count; i++)
            {
                EventEvidence evidence = record.Events[i].Evidence;
                if (evidence.Family == EventFamilies.Threshold && evidence.TargetIdentity == "THROTTLING")
                    crossings.Add(evidence);
            }

            return crossings;
        }

        /// <summary>
        /// Returns the runtime unit opened at the given source position.
        /// </summary>
        /// <param name="record">The frozen record.</param>
        /// <param name="position">The source position.</param>
        /// <returns>The runtime-unit record.</returns>
        private static RuntimeUnitRecord UnitAt(ExecutionRecord record, SourcePosition position)
        {
            for (int i = 0; i < record.Units.Count; i++)
            {
                if (record.Units[i].Opening.Position == position)
                    return record.Units[i];
            }

            Assert.Fail("No runtime unit at position " + position.LineNumber + ".");

            return null;
        }

        /// <summary>
        /// Counts the events belonging to the given family.
        /// </summary>
        /// <param name="record">The frozen record.</param>
        /// <param name="family">The family token.</param>
        /// <returns>The event count.</returns>
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
        /// Counts the events carrying the given subtype.
        /// </summary>
        /// <param name="record">The frozen record.</param>
        /// <param name="subtype">The subtype token.</param>
        /// <returns>The event count.</returns>
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
        /// A Core line assigning a constant to a register.
        /// </summary>
        /// <param name="identity">The stable Core-line identity.</param>
        /// <param name="target">The register the line writes.</param>
        /// <param name="constant">The constant operand.</param>
        /// <returns>The Core line.</returns>
        private static CoreLine AssignConstant(string identity, CoreRegister target, int constant)
        {
            return new CoreLine(identity, new CoreLineOperation(CoreLineOperator.Assign, target, OperandSpec.FromConstant(constant)));
        }

        /// <summary>
        /// A Core line multiplying a register by a constant.
        /// </summary>
        /// <param name="identity">The stable Core-line identity.</param>
        /// <param name="target">The register the line writes.</param>
        /// <param name="constant">The constant operand.</param>
        /// <returns>The Core line.</returns>
        private static CoreLine MultiplyConstant(string identity, CoreRegister target, int constant)
        {
            return new CoreLine(identity, new CoreLineOperation(CoreLineOperator.Multiply, target, OperandSpec.FromConstant(constant)));
        }

        /// <summary>
        /// A Core line adding one register to another.
        /// </summary>
        /// <param name="identity">The stable Core-line identity.</param>
        /// <param name="target">The register the line writes.</param>
        /// <param name="source">The register supplying the operand.</param>
        /// <returns>The Core line.</returns>
        private static CoreLine AddRegister(string identity, CoreRegister target, CoreRegister source)
        {
            return new CoreLine(identity, new CoreLineOperation(CoreLineOperator.Add, target, OperandSpec.FromRegister(source)));
        }

        /// <summary>
        /// Runs one request through a fresh scheduler and builder.
        /// </summary>
        /// <param name="request">The request to execute.</param>
        /// <returns>The frozen record.</returns>
        private static ExecutionRecord Execute(ExecutionRequest request)
        {
            return new ExecutionScheduler(new ExecutionTraceBuilder()).Execute(request);
        }

        /// <summary>
        /// The empty installed-Dependency list.
        /// </summary>
        /// <returns>The empty list.</returns>
        private static List<DependencyInstance> NoDependencies()
        {
            return new List<DependencyInstance>();
        }

        /// <summary>
        /// The standard reproduction revision stamps.
        /// </summary>
        /// <returns>The stamps.</returns>
        private static List<RevisionStamp> StandardStamps()
        {
            return new List<RevisionStamp>
            {
                new RevisionStamp("Content Catalog", "0.1.0"),
                new RevisionStamp("Random Service", "iterate-rng-1")
            };
        }

        /// <summary>
        /// The all-zero initial register state.
        /// </summary>
        /// <returns>The initial state.</returns>
        private static InitialExecutionState ZeroState()
        {
            return new InitialExecutionState(new ValueAmount(0), new SignalValue(0), new ScoreValue(0));
        }
    }
}
