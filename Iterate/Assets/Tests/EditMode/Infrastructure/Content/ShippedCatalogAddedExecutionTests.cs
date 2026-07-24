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
    /// The child's conformance evidence over the real frozen post-fix catalog: OVERCLOCK and ALIGN
    /// as active Directive pragmas and LOOP UNROLLER and BRANCH PREDICTOR installed, each driven
    /// end-to-end on shipped Instructions and Structures with hand-traced register outcomes — the
    /// OVERCLOCK qualifier fix observable as a Core line that never qualifies, the selected-host
    /// re-application inside a descendant, ALIGN landing before the trailing Core output, and the
    /// richest combined arrangement double-running structurally equal.
    /// </summary>
    public sealed class ShippedCatalogAddedExecutionTests
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
        public void Overclock_DuplicatesTheFirstPlayerValueGain_NeverTheCoreLine()
        {
            // Core Value = 1 → 1; WB-INS-002 (Value += 2) → 3; the OVERCLOCK descendant → 5.
            InstanceIDSource ids = new InstanceIDSource();
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForCore(new SourcePosition(1), AssignConstant("core-01", CoreRegister.Value, 1)),
                SourceSlot.ForInstruction(new SourcePosition(2), Instruction("WB-INS-002", ids))
            });

            ExecutionRecord record = Execute(Request(arrangement, NoDependencies(), Pragmas("WB-DIR-001", 900)));

            Assert.AreEqual(new ValueAmount(5), record.FinalState.FinalValue);
            Assert.AreEqual(3, record.Units.Count);
            Assert.AreEqual(1, CountSubtype(record, ExecutionEventSubtypes.AddedExecutionRequested));
            Assert.AreEqual(1, record.SafetyCounts.AddedDescendants);
            Assert.AreEqual(1, record.SafetyCounts.LineageDepthHighWater);
        }

        [Test]
        public void Overclock_CoreValueGain_NearMissesThePlayerInstructionQualifier()
        {
            InstanceIDSource ids = new InstanceIDSource();
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForCore(new SourcePosition(1), AssignConstant("core-01", CoreRegister.Value, 1)),
                SourceSlot.ForInstruction(new SourcePosition(2), Instruction("WB-INS-002", ids))
            });

            ExecutionRecord record = Execute(Request(arrangement, NoDependencies(), Pragmas("WB-DIR-001", 900)));

            EventEvidence nearMiss = FindInUnit(record, record.Units[0], ExecutionEventSubtypes.EffectFailedToQualify);

            Assert.AreEqual("OPERATION_CLASS:PLAYER_INSTRUCTION", nearMiss.DispositionReason);
            Assert.AreEqual(new InstanceID(900), nearMiss.EffectOriginInstance);
        }

        [Test]
        public void LoopUnroller_DuplicatesTheFirstRepeatChildOnly()
        {
            // WB-STR-001 Repeat 2 over WB-INS-002: iteration 1 child 0 → 2, its descendant → 4,
            // iteration 2 child → 6; iteration 2 is unduplicated.
            InstanceIDSource ids = new InstanceIDSource();
            StructureInstance repeat = Structure("WB-STR-001", 10);
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForStructureHeader(new SourcePosition(1), repeat),
                SourceSlot.ForContainedInstruction(new SourcePosition(2), repeat, Instruction("WB-INS-002", ids))
            });

            ExecutionRecord record = Execute(Request(arrangement, Installed("WB-DEP-009", 101), NoPragmas()));

            Assert.AreEqual(new ValueAmount(6), record.FinalState.FinalValue);
            Assert.AreEqual(3, record.Units.Count);
            Assert.AreEqual(1, CountSubtype(record, ExecutionEventSubtypes.AddedExecutionRequested));
        }

        [Test]
        public void LoopUnrollerWithStandardLibrary_ReAppliesTheSelectedHostInsideTheDescendant()
        {
            // WB-DEP-001 commits on iteration 1's child (0 + 2 + 1 = 3), re-applies in the LOOP
            // UNROLLER descendant (3 + 2 + 1 = 6) and again in iteration 2 (6 + 2 + 1 = 9) — one
            // commitment across the execution.
            InstanceIDSource ids = new InstanceIDSource();
            StructureInstance repeat = Structure("WB-STR-001", 10);
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForStructureHeader(new SourcePosition(1), repeat),
                SourceSlot.ForContainedInstruction(new SourcePosition(2), repeat, Instruction("WB-INS-002", ids))
            });
            List<DependencyInstance> installed = Installed("WB-DEP-009", 101);
            installed.Add(Dependency("WB-DEP-001", 100));

            ExecutionRecord record = Execute(Request(arrangement, installed, NoPragmas()));

            Assert.AreEqual(new ValueAmount(9), record.FinalState.FinalValue);
            Assert.AreEqual(3, record.Units.Count);
            Assert.AreEqual(1, CountCommitmentsBy(record, new InstanceID(100)), "the selected host commits exactly once");
        }

        [Test]
        public void BranchPredictor_EstablishesAtConditionTrue_AndResolvesAfterTheChild()
        {
            // WB-STR-002 (If Value is even) is TRUE at the neutral reset; its child WB-INS-002 runs
            // 0 → 2, then the pending request's descendant runs 2 → 4.
            InstanceIDSource ids = new InstanceIDSource();
            StructureInstance condition = Structure("WB-STR-002", 10);
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForStructureHeader(new SourcePosition(1), condition),
                SourceSlot.ForContainedInstruction(new SourcePosition(2), condition, Instruction("WB-INS-002", ids))
            });

            ExecutionRecord record = Execute(Request(arrangement, Installed("WB-DEP-010", 102), NoPragmas()));

            Assert.AreEqual(new ValueAmount(4), record.FinalState.FinalValue);
            Assert.AreEqual(2, record.Units.Count);
            Assert.AreEqual(ActivationKind.AddedExecution, record.Units[1].Opening.Activation);
            Assert.AreEqual(record.Units[0].Identity, record.Units[1].Opening.ParentUnit);
        }

        [Test]
        public void BranchPredictor_FalseCondition_EstablishesNothing()
        {
            // WB-STR-004 (If Value >= 10) is FALSE at the neutral reset, so its child is skipped.
            InstanceIDSource ids = new InstanceIDSource();
            StructureInstance condition = Structure("WB-STR-004", 10);
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForStructureHeader(new SourcePosition(1), condition),
                SourceSlot.ForContainedInstruction(new SourcePosition(2), condition, Instruction("WB-INS-002", ids))
            });

            ExecutionRecord record = Execute(Request(arrangement, Installed("WB-DEP-010", 102), NoPragmas()));

            Assert.AreEqual(new ValueAmount(0), record.FinalState.FinalValue);
            Assert.AreEqual(0, CountSubtype(record, ExecutionEventSubtypes.AddedExecutionRequested));
            Assert.AreEqual(0, CountSubtype(record, ExecutionEventSubtypes.AddedExecutionCancelled));
        }

        [Test]
        public void Align_OddPlayerValue_GainsOneBeforeTheCoreOutputLine()
        {
            // WB-INS-003 (Value += 3) → 3 is odd, so ALIGN adds 1 at the boundary → 4; the trailing
            // Core line then reads Value into Score.
            InstanceIDSource ids = new InstanceIDSource();
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForInstruction(new SourcePosition(1), Instruction("WB-INS-003", ids)),
                SourceSlot.ForCore(new SourcePosition(2), AssignRegister("core-out", CoreRegister.Score, CoreRegister.Value))
            });

            ExecutionRecord record = Execute(Request(arrangement, NoDependencies(), Pragmas("WB-DIR-002", 950)));

            Assert.AreEqual(new ValueAmount(4), record.FinalState.FinalValue);
            Assert.AreEqual(new ScoreValue(4), record.FinalState.FinalOutput);
            Assert.AreEqual(1, CountSubtype(record, ExecutionEventSubtypes.BoundaryEffectRequested));
        }

        [Test]
        public void Align_EvenPlayerValue_TakesTheParityNearMissOnly()
        {
            // WB-INS-002 (Value += 2) → 2 is even, so ALIGN near-misses and Score reads 2.
            InstanceIDSource ids = new InstanceIDSource();
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForInstruction(new SourcePosition(1), Instruction("WB-INS-002", ids)),
                SourceSlot.ForCore(new SourcePosition(2), AssignRegister("core-out", CoreRegister.Score, CoreRegister.Value))
            });

            ExecutionRecord record = Execute(Request(arrangement, NoDependencies(), Pragmas("WB-DIR-002", 950)));

            Assert.AreEqual(new ValueAmount(2), record.FinalState.FinalValue);
            Assert.AreEqual(new ScoreValue(2), record.FinalState.FinalOutput);
            Assert.AreEqual(0, CountSubtype(record, ExecutionEventSubtypes.BoundaryEffectRequested));
            Assert.AreEqual("PARITY:ODD", FindGlobal(record, ExecutionEventSubtypes.EffectFailedToQualify).DispositionReason);
        }

        [Test]
        public void RichestCombination_LandsTheHandTracedOutcome()
        {
            ExecutionRecord record = Execute(RichestRequest());

            Assert.AreEqual(new ValueAmount(16), record.FinalState.FinalValue);
            Assert.AreEqual(new ScoreValue(16), record.FinalState.FinalOutput);
            Assert.AreEqual(7, record.Units.Count, "six player-side units plus the Core output line");
        }

        [Test]
        public void RichestCombination_EveryCreatorFiresAtMostOnce()
        {
            ExecutionRecord record = Execute(RichestRequest());

            Assert.AreEqual(3, CountSubtype(record, ExecutionEventSubtypes.AddedExecutionRequested));
            Assert.AreEqual(1, CountSubtype(record, ExecutionEventSubtypes.BoundaryEffectRequested));
            Assert.AreEqual(1, CountCommitmentsBy(record, new InstanceID(100)));
            Assert.AreEqual(0, CountSubtype(record, ExecutionEventSubtypes.AddedExecutionCancelled));
        }

        [Test]
        public void RichestCombination_SafetyCountsAreReal()
        {
            ExecutionRecord record = Execute(RichestRequest());

            Assert.AreEqual(3, record.SafetyCounts.AddedDescendants);
            Assert.AreEqual(1, record.SafetyCounts.LineageDepthHighWater);
            Assert.AreEqual(7, record.SafetyCounts.SourceExecutionUnits, "six player-side units plus the Core output line");
        }

        [Test]
        public void RichestCombination_DoubleRunsStructurallyEqual()
        {
            ExecutionRecord first = Execute(RichestRequest());
            ExecutionRecord second = Execute(RichestRequest());

            Assert.AreEqual(first.Events.Count, second.Events.Count);
            Assert.AreEqual(first.FinalState, second.FinalState);
            Assert.AreEqual(first.SafetyCounts, second.SafetyCounts);
            for (int i = 0; i < first.Events.Count; i++)
            {
                Assert.AreEqual(first.Events[i].Evidence, second.Events[i].Evidence, "event " + i);
            }

            for (int i = 0; i < first.Units.Count; i++)
            {
                Assert.AreEqual(first.Units[i].Opening, second.Units[i].Opening, "unit " + i);
            }
        }

        /// <summary>
        /// The richest shipped arrangement: WB-STR-002 (`If Value is even`) over WB-INS-002, then
        /// WB-STR-001 (`Repeat 2`) over WB-INS-002, then a Core output line, with WB-DEP-001/009/010
        /// installed and both the OVERCLOCK and ALIGN pragmas active.
        /// </summary>
        /// <returns>The assembled request.</returns>
        private ExecutionRequest RichestRequest()
        {
            // Condition TRUE (0 is even) establishes BRANCH PREDICTOR on its child.
            // Child:                    0 + 2 + 1 (STANDARD LIBRARY commits, host locked) =  3
            //   OVERCLOCK descendant:   3 + 2 + 1 (selected host re-applies)               =  6
            //   BRANCH PREDICTOR desc:  6 + 2 + 1 (same host again)                        =  9
            // Repeat iteration 1:       9 + 2     (different host — no re-application)      = 11
            //   LOOP UNROLLER desc:    11 + 2                                               = 13
            // Repeat iteration 2:      13 + 2                                               = 15
            // Boundary: 15 is odd, so ALIGN adds 1                                          = 16
            // Core output line then reads Value into Score                                  = 16
            // Seven runtime units open: the Condition child, its two descendants, both Repeat
            // iterations, the LOOP UNROLLER descendant, and the Core output line itself.
            InstanceIDSource ids = new InstanceIDSource();
            StructureInstance condition = Structure("WB-STR-002", 10);
            StructureInstance repeat = Structure("WB-STR-001", 11);
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForStructureHeader(new SourcePosition(1), condition),
                SourceSlot.ForContainedInstruction(new SourcePosition(2), condition, Instruction("WB-INS-002", ids)),
                SourceSlot.ForStructureHeader(new SourcePosition(3), repeat),
                SourceSlot.ForContainedInstruction(new SourcePosition(4), repeat, Instruction("WB-INS-002", ids)),
                SourceSlot.ForCore(new SourcePosition(5), AssignRegister("core-out", CoreRegister.Score, CoreRegister.Value))
            });

            List<DependencyInstance> installed = new List<DependencyInstance>
            {
                Dependency("WB-DEP-001", 100),
                Dependency("WB-DEP-009", 101),
                Dependency("WB-DEP-010", 102)
            };
            List<DirectiveInstance> pragmas = new List<DirectiveInstance>
            {
                Directive("WB-DIR-001", 900),
                Directive("WB-DIR-002", 950)
            };

            return Request(arrangement, installed, pragmas);
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
        /// Wraps a real frozen Instruction definition in an instance with the next identity.
        /// </summary>
        /// <param name="id">The Instruction's surrogate-key identity.</param>
        /// <param name="ids">The instance-identity source.</param>
        /// <returns>The Instruction instance.</returns>
        private InstructionInstance Instruction(string id, InstanceIDSource ids)
        {
            Assert.IsTrue(_catalog.TryGetInstruction(new InstructionID(id), out InstructionDefinition definition), id);
            return new InstructionInstance(ids.Next(), definition, null);
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
        /// Counts the commitment events a named effect origin produced.
        /// </summary>
        /// <param name="record">The frozen record.</param>
        /// <param name="origin">The effect origin instance.</param>
        /// <returns>The number of commitments.</returns>
        private static int CountCommitmentsBy(ExecutionRecord record, InstanceID origin)
        {
            int count = 0;
            for (int i = 0; i < record.Events.Count; i++)
            {
                EventEvidence evidence = record.Events[i].Evidence;
                if (evidence.Subtype == ExecutionEventSubtypes.EffectCommitted && evidence.EffectOriginInstance == origin)
                    count++;
            }

            return count;
        }

        /// <summary>
        /// Finds the first record-wide event carrying the subtype.
        /// </summary>
        /// <param name="record">The frozen record.</param>
        /// <param name="subtype">The subtype token.</param>
        /// <returns>The event's evidence.</returns>
        private static EventEvidence FindGlobal(ExecutionRecord record, string subtype)
        {
            for (int i = 0; i < record.Events.Count; i++)
            {
                if (record.Events[i].Evidence.Subtype == subtype)
                    return record.Events[i].Evidence;
            }

            Assert.Fail($"No event carries subtype {subtype}.");
            return null;
        }

        /// <summary>
        /// Finds the first event carrying the subtype inside one unit.
        /// </summary>
        /// <param name="record">The frozen record.</param>
        /// <param name="unit">The unit to scan.</param>
        /// <param name="subtype">The subtype token.</param>
        /// <returns>The event's evidence.</returns>
        private static EventEvidence FindInUnit(ExecutionRecord record, RuntimeUnitRecord unit, string subtype)
        {
            for (int i = 0; i < unit.ChildEvents.Count; i++)
            {
                EventEvidence evidence = record.Events[unit.ChildEvents[i].Value - 1].Evidence;
                if (evidence.Subtype == subtype)
                    return evidence;
            }

            Assert.Fail($"The unit carries no event with subtype {subtype}.");
            return null;
        }
    }
}
