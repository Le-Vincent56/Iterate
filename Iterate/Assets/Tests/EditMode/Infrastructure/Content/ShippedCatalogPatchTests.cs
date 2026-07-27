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
    /// The child's conformance evidence over the real frozen post-fix catalog: each of the six shipped
    /// WB-PAT records and BURST OUTPUT (WB-DIR-003) executing on shipped Instructions, Structures, and
    /// Dependencies with hand-traced register outcomes stated in each Arrange comment — CONSTANT PATCH
    /// composing with STANDARD LIBRARY in the CAB-EVT-543 order, ECHO firing per Repeat iteration,
    /// TERMINAL on the final occupied player line, TRUE eligible inside a succeeding Condition and
    /// ineligible on a rescued-from-FALSE host, FEEDBACK ordering ahead of OUTPUT CACHE and OUTPUT
    /// PIPELINE, PIPELINE's runtime adjacency created by an added execution and broken by an intervening
    /// unit, BURST OUTPUT locking the most recent qualifying Score host and resolving its branch before
    /// ALIGN and before the trailing Core output, the reachable BURST + FEEDBACK + ALIGN parity flip in
    /// both parities, a no-qualifying-event execution leaving no lock, and a combined arrangement
    /// double-running structurally equal.
    /// </summary>
    public sealed class ShippedCatalogPatchTests
    {
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
        public void ConstantPatch_FixedAdditionHost_RaisesTheConstantByOne()
        {
            // WB-INS-002 (Value += 2) with WB-PAT-001 attached: the persistent adjustment lands 3.
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForInstruction(new SourcePosition(1), Patched("WB-INS-002", 10, "WB-PAT-001", 60))
            });

            ExecutionRecord record = Execute(Request(arrangement, NoDependencies(), NoPragmas()));

            Assert.AreEqual(new ValueAmount(3), record.FinalState.FinalValue);
            Assert.AreEqual(1, CountSubtype(record, ExecutionEventSubtypes.PrimaryOperationModified));
        }

        [Test]
        public void ConstantPatch_WithStandardLibrary_ComposesPersistentBeforeSelectedHost()
        {
            // Authored 2 → CONSTANT PATCH +1 → STANDARD LIBRARY +1 lands 4; the Patch modifies first.
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForInstruction(new SourcePosition(1), Patched("WB-INS-002", 10, "WB-PAT-001", 60))
            });

            ExecutionRecord record = Execute(Request(arrangement, Installed("WB-DEP-001", 100), NoPragmas()));

            Assert.AreEqual(new ValueAmount(4), record.FinalState.FinalValue);
            List<int> modified = IndexesOfSubtype(record, ExecutionEventSubtypes.PrimaryOperationModified);
            Assert.AreEqual(2, modified.Count);
            Assert.AreEqual(new InstanceID(60), record.Events[modified[0]].Evidence.EffectOriginInstance, "the persistent Patch adjustment applies first");
            Assert.AreEqual(new InstanceID(100), record.Events[modified[1]].Evidence.EffectOriginInstance, "the selected-host modification applies second");
        }

        [Test]
        public void EchoPatch_EvenLineHost_ExecutesOneAdditionalTime()
        {
            // Core Value = 1; the position-2 host (WB-INS-002) adds 2 canonically and 2 in its
            // descendant: Value 1 → 3 → 5.
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForCore(new SourcePosition(1), AssignConstant("core-01", CoreRegister.Value, 1)),
                SourceSlot.ForInstruction(new SourcePosition(2), Patched("WB-INS-002", 10, "WB-PAT-002", 60))
            });

            ExecutionRecord record = Execute(Request(arrangement, NoDependencies(), NoPragmas()));

            Assert.AreEqual(new ValueAmount(5), record.FinalState.FinalValue);
            Assert.AreEqual(3, record.Units.Count);
            Assert.AreEqual(1, CountSubtype(record, ExecutionEventSubtypes.AddedExecutionRequested));
        }

        [Test]
        public void EchoPatch_InsideRepeatTwo_FiresOncePerIteration()
        {
            // WB-STR-001 (Repeat 2) over the even-line host: iteration 1 fires (0 → 2, descendant → 4),
            // iteration 2 fires (4 → 6, descendant → 8) — the SOURCE_EXECUTION scope pin.
            StructureInstance repeat = Structure("WB-STR-001", 110);
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForStructureHeader(new SourcePosition(1), repeat),
                SourceSlot.ForContainedInstruction(new SourcePosition(2), repeat, Patched("WB-INS-002", 10, "WB-PAT-002", 60))
            });

            ExecutionRecord record = Execute(Request(arrangement, NoDependencies(), NoPragmas()));

            Assert.AreEqual(new ValueAmount(8), record.FinalState.FinalValue);
            Assert.AreEqual(4, record.Units.Count);
            Assert.AreEqual(2, CountSubtype(record, ExecutionEventSubtypes.AddedExecutionRequested));
        }

        [Test]
        public void TerminalPatch_FinalOccupiedPlayerLine_ExecutesOneAdditionalTime()
        {
            // WB-INS-001 (Value += 1) at 1, the TERMINAL host (WB-INS-002) at 2 — the final occupied
            // player line, the empty at 3 not blocking — then a Core Score = Value line at 4:
            // Value 0 → 1 → 3 → 5, Score reads 5.
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForInstruction(new SourcePosition(1), Instruction("WB-INS-001", 20)),
                SourceSlot.ForInstruction(new SourcePosition(2), Patched("WB-INS-002", 10, "WB-PAT-003", 60)),
                SourceSlot.ForEmpty(new SourcePosition(3)),
                SourceSlot.ForCore(new SourcePosition(4), AssignRegister("core-04", CoreRegister.Score, CoreRegister.Value))
            });

            ExecutionRecord record = Execute(Request(arrangement, NoDependencies(), NoPragmas()));

            Assert.AreEqual(new ValueAmount(5), record.FinalState.FinalValue);
            Assert.AreEqual(new ScoreValue(5), record.FinalState.FinalOutput);
            Assert.AreEqual(1, CountSubtype(record, ExecutionEventSubtypes.AddedExecutionRequested));
        }

        [Test]
        public void TruePatch_InsideSucceedingCondition_ExecutesOneAdditionalTime()
        {
            // WB-STR-002 (If Value is even) is TRUE at the neutral reset (0 is even); its TRUE-patched
            // child adds 2 canonically and 2 in its descendant: Value 0 → 2 → 4.
            StructureInstance condition = Structure("WB-STR-002", 110);
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForStructureHeader(new SourcePosition(1), condition),
                SourceSlot.ForContainedInstruction(new SourcePosition(2), condition, Patched("WB-INS-002", 10, "WB-PAT-004", 60))
            });

            ExecutionRecord record = Execute(Request(arrangement, NoDependencies(), NoPragmas()));

            Assert.AreEqual(new ValueAmount(4), record.FinalState.FinalValue);
            Assert.AreEqual(2, record.Units.Count);
            Assert.AreEqual(1, CountSubtype(record, ExecutionEventSubtypes.AddedExecutionRequested));
        }

        [Test]
        public void TruePatch_RescuedFromFalseHost_IsIneligible()
        {
            // WB-STR-004 (If Value >= 10) is FALSE at the neutral reset; WB-DEP-007 rescues the skipped
            // host, which resolves carrying the retained FALSE outcome — TRUE PATCH near-misses, never
            // fires. Value 0 → 2.
            StructureInstance condition = Structure("WB-STR-004", 110);
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForStructureHeader(new SourcePosition(1), condition),
                SourceSlot.ForContainedInstruction(new SourcePosition(2), condition, Patched("WB-INS-002", 10, "WB-PAT-004", 60))
            });

            ExecutionRecord record = Execute(Request(arrangement, Installed("WB-DEP-007", 100), NoPragmas()));

            Assert.AreEqual(new ValueAmount(2), record.FinalState.FinalValue);
            Assert.AreEqual(0, CountSubtype(record, ExecutionEventSubtypes.AddedExecutionRequested));
            Assert.AreEqual("STRUCTURE_CONTEXT:INSIDE_SUCCEEDING_CONDITION", NearMissOf(record, new InstanceID(60)).DispositionReason);
        }

        [Test]
        public void FeedbackPatch_OrdersAheadOfOutputCacheAndOutputPipeline()
        {
            // Core Value = 3; the Score host (WB-INS-012) gains Score 0 → 3, firing three reactions in
            // the CAB-EVT-532 order: FEEDBACK PATCH (Value += 1 → 4), OUTPUT CACHE (Score += 2 → 5),
            // OUTPUT PIPELINE (Value += 2 → 6). Final Value 6, Score 5.
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForCore(new SourcePosition(1), AssignConstant("core-01", CoreRegister.Value, 3)),
                SourceSlot.ForInstruction(new SourcePosition(2), Patched("WB-INS-012", 10, "WB-PAT-005", 60))
            });
            List<DependencyInstance> installed = new List<DependencyInstance>
            {
                Dependency("WB-DEP-005", 100),
                Dependency("WB-DEP-011", 101)
            };

            ExecutionRecord record = Execute(Request(arrangement, installed, NoPragmas()));

            Assert.AreEqual(new ValueAmount(6), record.FinalState.FinalValue);
            Assert.AreEqual(new ScoreValue(5), record.FinalState.FinalScore);
            int feedback = IndexOfReactionQuantity(record, new InstanceID(60));
            int outputCache = IndexOfReactionQuantity(record, new InstanceID(100));
            int outputPipeline = IndexOfReactionQuantity(record, new InstanceID(101));
            Assert.Less(feedback, outputCache, "FEEDBACK PATCH resolves before OUTPUT CACHE");
            Assert.Less(outputCache, outputPipeline, "OUTPUT CACHE resolves before OUTPUT PIPELINE");
        }

        [Test]
        public void PipelinePatch_AfterScoreIncreasingPlayerInstruction_Fires()
        {
            // Core Value = 2; WB-INS-012 gains Score 0 → 2; the immediately following PIPELINE host is
            // adjacent to a successful player Score gain, so it fires: Value 2 → 4 → 6.
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForCore(new SourcePosition(1), AssignConstant("core-01", CoreRegister.Value, 2)),
                SourceSlot.ForInstruction(new SourcePosition(2), Instruction("WB-INS-012", 20)),
                SourceSlot.ForInstruction(new SourcePosition(3), Patched("WB-INS-002", 10, "WB-PAT-006", 60))
            });

            ExecutionRecord record = Execute(Request(arrangement, NoDependencies(), NoPragmas()));

            Assert.AreEqual(new ValueAmount(6), record.FinalState.FinalValue);
            Assert.AreEqual(new ScoreValue(2), record.FinalState.FinalScore);
            Assert.AreEqual(1, CountSubtype(record, ExecutionEventSubtypes.AddedExecutionRequested));
        }

        [Test]
        public void PipelinePatch_AdjacencyCreatedByAnAddedExecution_Fires()
        {
            // WB-INS-001 → Value 1; an ECHO-patched Score host (WB-INS-012) gains Score 0 → 1 and its
            // ECHO descendant gains Score 1 → 2 — an added execution as the immediately preceding
            // completed unit; the PIPELINE host then qualifies against it: Value 1 → 3 → 5.
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForInstruction(new SourcePosition(1), Instruction("WB-INS-001", 30)),
                SourceSlot.ForInstruction(new SourcePosition(2), Patched("WB-INS-012", 20, "WB-PAT-002", 61)),
                SourceSlot.ForInstruction(new SourcePosition(3), Patched("WB-INS-002", 10, "WB-PAT-006", 60))
            });

            ExecutionRecord record = Execute(Request(arrangement, NoDependencies(), NoPragmas()));

            Assert.AreEqual(new ValueAmount(5), record.FinalState.FinalValue);
            Assert.AreEqual(new ScoreValue(2), record.FinalState.FinalScore);
            Assert.AreEqual(2, CountSubtype(record, ExecutionEventSubtypes.AddedExecutionRequested));
            Assert.AreEqual(ActivationKind.AddedExecution, record.Units[2].Opening.Activation, "the ECHO descendant precedes the observing host");
        }

        [Test]
        public void PipelinePatch_InterveningSuccessfulNonScoreUnit_Breaks()
        {
            // Core Value = 2; WB-INS-012 gains Score; WB-INS-006 (Signal += 1) is a successful
            // non-qualifying unit that replaces the predecessor, so the PIPELINE host near-misses.
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForCore(new SourcePosition(1), AssignConstant("core-01", CoreRegister.Value, 2)),
                SourceSlot.ForInstruction(new SourcePosition(2), Instruction("WB-INS-012", 20)),
                SourceSlot.ForInstruction(new SourcePosition(3), Instruction("WB-INS-006", 21)),
                SourceSlot.ForInstruction(new SourcePosition(4), Patched("WB-INS-002", 10, "WB-PAT-006", 60))
            });

            ExecutionRecord record = Execute(Request(arrangement, NoDependencies(), NoPragmas()));

            Assert.AreEqual(new ValueAmount(4), record.FinalState.FinalValue);
            Assert.AreEqual(0, CountSubtype(record, ExecutionEventSubtypes.AddedExecutionRequested));
            Assert.AreEqual("STRUCTURE_CONTEXT:ADJACENT_AFTER_SUCCESSFUL_SCORE", NearMissOf(record, new InstanceID(60)).DispositionReason);
        }

        [Test]
        public void BurstOutput_LocksScoreHost_ResolvesBranchBeforeAlignAndCore()
        {
            // Core Value = 3; WB-INS-012 gains Score 0 → 3 (BURST locks it); the Burst branch re-reads
            // the pre-ALIGN Value 3 (Score 3 → 6); ALIGN then aligns the odd Value 3 → 4; the trailing
            // Core Score = Value reads the aligned 4 (Score 6 → 10). Final Value 4, Score 10 — the
            // CAB-EVT-129 order signature (an ALIGN-first order would land Score 11).
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForCore(new SourcePosition(1), AssignConstant("core-01", CoreRegister.Value, 3)),
                SourceSlot.ForInstruction(new SourcePosition(2), Instruction("WB-INS-012", 20)),
                SourceSlot.ForCore(new SourcePosition(3), AddRegister("core-03", CoreRegister.Score, CoreRegister.Value))
            });

            ExecutionRecord record = Execute(Request(arrangement, NoDependencies(), Pragmas("WB-DIR-003", 900, "WB-DIR-002", 950)));

            Assert.AreEqual(new ValueAmount(4), record.FinalState.FinalValue);
            Assert.AreEqual(new ScoreValue(10), record.FinalState.FinalScore);
            Assert.AreEqual(1, CountSubtype(record, ExecutionEventSubtypes.AddedExecutionTargetLocked));
            Assert.AreEqual(1, CountSubtype(record, ExecutionEventSubtypes.AddedExecutionRequested));
            Assert.AreEqual(1, CountSubtype(record, ExecutionEventSubtypes.BoundaryEffectRequested));
        }

        [Test]
        public void BurstOutput_LockedHostFeedbackFlipsToOdd_AlignQualifies()
        {
            // Core Value = 1; the FEEDBACK-patched Score host gains Score 0 → 1 and FEEDBACK lifts
            // Value 1 → 2; the Burst branch re-gains Score 1 → 3 and FEEDBACK lifts Value 2 → 3 (odd),
            // so ALIGN's fresh offer qualifies → Value 4. Final Value 4, Score 3.
            ExecutionRecord record = Execute(ParityFlipRequest(1));

            Assert.AreEqual(new ValueAmount(4), record.FinalState.FinalValue);
            Assert.AreEqual(new ScoreValue(3), record.FinalState.FinalScore);
            Assert.AreEqual(1, CountSubtype(record, ExecutionEventSubtypes.BoundaryEffectRequested), "ALIGN qualifies on the flipped parity");
            Assert.AreEqual(1, CountSubtype(record, ExecutionEventSubtypes.AddedExecutionTargetLocked));
        }

        [Test]
        public void BurstOutput_LockedHostFeedbackFlipsToEven_AlignNearMisses()
        {
            // Core Value = 2; the host gains Score 0 → 2 and FEEDBACK lifts Value 2 → 3 (ALIGN would
            // qualify pre-Burst — the discarded creator offer); the Burst branch re-gains Score 2 → 5
            // and FEEDBACK lifts Value 3 → 4 (even), so ALIGN's fresh offer near-misses exactly once.
            // Final Value 4, Score 5.
            ExecutionRecord record = Execute(ParityFlipRequest(2));

            Assert.AreEqual(new ValueAmount(4), record.FinalState.FinalValue);
            Assert.AreEqual(new ScoreValue(5), record.FinalState.FinalScore);
            Assert.AreEqual(0, CountSubtype(record, ExecutionEventSubtypes.BoundaryEffectRequested));
            Assert.AreEqual(1, CountNearMissReason(record, "PARITY:ODD"), "exactly one near-miss across the whole boundary");
            Assert.AreEqual(1, CountSubtype(record, ExecutionEventSubtypes.AddedExecutionTargetLocked));
        }

        [Test]
        public void BurstOutput_NoQualifyingScore_LeavesNoLockNoRequest()
        {
            // A Value-only player Instruction never qualifies the Score lock: no token, no request, the
            // ONCE allowance intact (no commitment). Value 0 → 2.
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForInstruction(new SourcePosition(1), Instruction("WB-INS-002", 20))
            });

            ExecutionRecord record = Execute(Request(arrangement, NoDependencies(), Pragmas("WB-DIR-003", 900)));

            Assert.AreEqual(new ValueAmount(2), record.FinalState.FinalValue);
            Assert.AreEqual(0, CountSubtype(record, ExecutionEventSubtypes.AddedExecutionTargetLocked));
            Assert.AreEqual(0, CountSubtype(record, ExecutionEventSubtypes.AddedExecutionRequested));
        }

        [Test]
        public void BurstWithFeedbackAndAlign_DoubleRunsStructurallyEqual()
        {
            ExecutionRecord first = Execute(ParityFlipRequest(2));
            ExecutionRecord second = Execute(ParityFlipRequest(2));

            Assert.AreEqual(first.Events.Count, second.Events.Count);
            Assert.AreEqual(first.Units.Count, second.Units.Count);
            Assert.AreEqual(first.FinalState, second.FinalState);
            Assert.AreEqual(first.SafetyCounts, second.SafetyCounts);
            for (int i = 0; i < first.Events.Count; i++)
            {
                Assert.AreEqual(first.Events[i].Evidence, second.Events[i].Evidence, "event " + i);
            }
        }

        /// <summary>
        /// The BURST + FEEDBACK + ALIGN parity fixture: Core seeds Value, the FEEDBACK-patched Score
        /// host (WB-INS-012) is locked by BURST OUTPUT, and both the Burst branch's own FEEDBACK gain
        /// and ALIGN read the flipped parity. An odd seed lands ALIGN qualifying, an even seed lands the
        /// single near-miss.
        /// </summary>
        /// <param name="seed">The Core-assigned starting Value.</param>
        /// <returns>The assembled request.</returns>
        private ExecutionRequest ParityFlipRequest(int seed)
        {
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForCore(new SourcePosition(1), AssignConstant("core-01", CoreRegister.Value, seed)),
                SourceSlot.ForInstruction(new SourcePosition(2), Patched("WB-INS-012", 10, "WB-PAT-005", 60))
            });

            return Request(arrangement, NoDependencies(), Pragmas("WB-DIR-003", 900, "WB-DIR-002", 950));
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
            return new InstructionInstance(new InstanceID(instance), definition, null);
        }

        /// <summary>
        /// Wraps a real frozen Instruction definition in an instance carrying a real frozen Patch.
        /// </summary>
        /// <param name="instructionID">The host Instruction's surrogate-key identity.</param>
        /// <param name="hostInstance">The host instance identity value.</param>
        /// <param name="patchID">The Patch's surrogate-key identity.</param>
        /// <param name="patchInstance">The Patch instance identity value.</param>
        /// <returns>The patched Instruction instance.</returns>
        private InstructionInstance Patched(
            string instructionID,
            int hostInstance,
            string patchID,
            int patchInstance)
        {
            Assert.IsTrue(_catalog.TryGetInstruction(new InstructionID(instructionID), out InstructionDefinition definition), instructionID);
            Assert.IsTrue(_catalog.TryGetPatch(new PatchID(patchID), out PatchDefinition patch), patchID);
            return new InstructionInstance(new InstanceID(hostInstance), definition, new PatchInstance(new InstanceID(patchInstance), patch));
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
        /// Wraps a real frozen Directive definition in an instance.
        /// </summary>
        /// <param name="id">The Directive's surrogate-key identity.</param>
        /// <param name="instance">The instance identity value.</param>
        /// <returns>The Directive instance.</returns>
        private DirectiveInstance Directive(string id, int instance)
        {
            Assert.IsTrue(_catalog.TryGetDirective(new DirectiveID(id), out DirectiveDefinition definition), id);
            return new DirectiveInstance(new InstanceID(instance), definition);
        }

        /// <summary>
        /// One real frozen Dependency wrapped as a single-entry installed list.
        /// </summary>
        /// <param name="id">The Dependency's surrogate-key identity.</param>
        /// <param name="instance">The instance identity value.</param>
        /// <returns>The installed list.</returns>
        private List<DependencyInstance> Installed(string id, int instance)
        {
            return new List<DependencyInstance> { Dependency(id, instance) };
        }

        /// <summary>
        /// One real frozen Directive wrapped as a single-entry pragma list.
        /// </summary>
        /// <param name="id">The Directive's surrogate-key identity.</param>
        /// <param name="instance">The instance identity value.</param>
        /// <returns>The pragma list.</returns>
        private List<DirectiveInstance> Pragmas(string id, int instance)
        {
            return new List<DirectiveInstance> { Directive(id, instance) };
        }

        /// <summary>
        /// Two real frozen Directives wrapped as a pragma list in the given order.
        /// </summary>
        /// <param name="firstID">The first Directive's surrogate-key identity.</param>
        /// <param name="firstInstance">The first instance identity value.</param>
        /// <param name="secondID">The second Directive's surrogate-key identity.</param>
        /// <param name="secondInstance">The second instance identity value.</param>
        /// <returns>The pragma list.</returns>
        private List<DirectiveInstance> Pragmas(
            string firstID,
            int firstInstance,
            string secondID,
            int secondInstance)
        {
            return new List<DirectiveInstance> { Directive(firstID, firstInstance), Directive(secondID, secondInstance) };
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
        /// The empty pragma list.
        /// </summary>
        /// <returns>The empty list.</returns>
        private static List<DirectiveInstance> NoPragmas()
        {
            return new List<DirectiveInstance>();
        }

        /// <summary>
        /// A Core line assigning a constant to a register.
        /// </summary>
        /// <param name="identity">The Core line's identity.</param>
        /// <param name="target">The target register.</param>
        /// <param name="constant">The constant operand.</param>
        /// <returns>The Core line.</returns>
        private static CoreLine AssignConstant(string identity, CoreRegister target, int constant)
        {
            return new CoreLine(identity, new CoreLineOperation(CoreLineOperator.Assign, target, OperandSpec.FromConstant(constant)));
        }

        /// <summary>
        /// A Core line assigning one register's current value to another.
        /// </summary>
        /// <param name="identity">The Core line's identity.</param>
        /// <param name="target">The target register.</param>
        /// <param name="source">The source register.</param>
        /// <returns>The Core line.</returns>
        private static CoreLine AssignRegister(string identity, CoreRegister target, CoreRegister source)
        {
            return new CoreLine(identity, new CoreLineOperation(CoreLineOperator.Assign, target, OperandSpec.FromRegister(source)));
        }

        /// <summary>
        /// A Core line adding one register's current value into another.
        /// </summary>
        /// <param name="identity">The Core line's identity.</param>
        /// <param name="target">The target register.</param>
        /// <param name="source">The source register.</param>
        /// <returns>The Core line.</returns>
        private static CoreLine AddRegister(string identity, CoreRegister target, CoreRegister source)
        {
            return new CoreLine(identity, new CoreLineOperation(CoreLineOperator.Add, target, OperandSpec.FromRegister(source)));
        }

        /// <summary>
        /// Assembles a request over the arrangement with the given installed Dependencies and active
        /// Directive pragmas.
        /// </summary>
        /// <param name="arrangement">The source arrangement.</param>
        /// <param name="installed">The installed Dependency instances.</param>
        /// <param name="pragmas">The active Directive pragmas.</param>
        /// <returns>The assembled request.</returns>
        private static ExecutionRequest Request(
            SourceArrangement arrangement,
            List<DependencyInstance> installed,
            List<DirectiveInstance> pragmas)
        {
            CompiledSource source = new CompiledSource(
                arrangement,
                pragmas,
                new CompilationCostBreakdown(CompilationClassification.Initial, 0, true, 0, new List<CostModifierEntry>(), 0, false));

            return new ExecutionRequest(source, StandardConfiguration(), StandardStamps(), ZeroState(), installed);
        }

        /// <summary>
        /// Executes a request on a fresh scheduler and builder.
        /// </summary>
        /// <param name="request">The request to execute.</param>
        /// <returns>The frozen record.</returns>
        private static ExecutionRecord Execute(ExecutionRequest request)
        {
            return new ExecutionScheduler(new ExecutionTraceBuilder()).Execute(request);
        }

        /// <summary>
        /// The standard Process execution configuration.
        /// </summary>
        /// <returns>The configuration.</returns>
        private static ProcessExecutionConfiguration StandardConfiguration()
        {
            return new ProcessExecutionConfiguration(
                "exec",
                "compilation",
                "source-rev",
                "process",
                "core",
                "rule-config",
                "session-seed",
                new ProcessThresholds(new ScoreValue(20), new ScoreValue(30), new ScoreValue(36)));
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

        /// <summary>
        /// Counts the events carrying the subtype across the record.
        /// </summary>
        /// <param name="record">The frozen record.</param>
        /// <param name="subtype">The subtype token to count.</param>
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
        /// Counts the near-misses carrying the failed-requirement reason across the record.
        /// </summary>
        /// <param name="record">The frozen record.</param>
        /// <param name="reason">The failed-requirement reason string.</param>
        /// <returns>The count.</returns>
        private static int CountNearMissReason(ExecutionRecord record, string reason)
        {
            int count = 0;
            for (int i = 0; i < record.Events.Count; i++)
            {
                EventEvidence evidence = record.Events[i].Evidence;
                if (evidence.Subtype == ExecutionEventSubtypes.EffectFailedToQualify && evidence.DispositionReason == reason)
                    count++;
            }

            return count;
        }

        /// <summary>
        /// Returns the single near-miss a named effect origin produced.
        /// </summary>
        /// <param name="record">The frozen record.</param>
        /// <param name="origin">The effect origin instance.</param>
        /// <returns>The near-miss evidence.</returns>
        private static EventEvidence NearMissOf(ExecutionRecord record, InstanceID origin)
        {
            for (int i = 0; i < record.Events.Count; i++)
            {
                EventEvidence evidence = record.Events[i].Evidence;
                if (evidence.Subtype == ExecutionEventSubtypes.EffectFailedToQualify && evidence.EffectOriginInstance == origin)
                    return evidence;
            }

            Assert.Fail($"No near-miss carries origin {origin}.");
            return null;
        }

        /// <summary>
        /// Returns every record-wide index carrying the subtype, in order.
        /// </summary>
        /// <param name="record">The frozen record.</param>
        /// <param name="subtype">The subtype token.</param>
        /// <returns>The matching indexes.</returns>
        private static List<int> IndexesOfSubtype(ExecutionRecord record, string subtype)
        {
            List<int> indexes = new List<int>();
            for (int i = 0; i < record.Events.Count; i++)
            {
                if (record.Events[i].Evidence.Subtype == subtype)
                    indexes.Add(i);
            }

            return indexes;
        }

        /// <summary>
        /// Returns the record-wide index of the quantity event a named effect origin produced.
        /// </summary>
        /// <param name="record">The frozen record.</param>
        /// <param name="origin">The effect origin instance.</param>
        /// <returns>The index.</returns>
        private static int IndexOfReactionQuantity(ExecutionRecord record, InstanceID origin)
        {
            for (int i = 0; i < record.Events.Count; i++)
            {
                EventEvidence evidence = record.Events[i].Evidence;
                if (evidence.Subtype == ExecutionEventSubtypes.QuantityChanged && evidence.EffectOriginInstance == origin)
                    return i;
            }

            Assert.Fail($"No reaction quantity event carries origin {origin}.");
            return -1;
        }
    }
}
