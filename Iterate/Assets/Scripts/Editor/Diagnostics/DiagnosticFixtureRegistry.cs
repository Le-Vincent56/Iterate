using System;
using System.Collections.Generic;
using Iterate.Domain.Compilation;
using Iterate.Domain.Content;
using Iterate.Domain.Execution;
using Iterate.Domain.Trace;
using Iterate.Domain.Values;

namespace Iterate.Editor.Diagnostics
{
    /// <summary>
    /// The named diagnostic fixture set: five compositions built from the frozen shipped catalog, used
    /// to generate execution records in-session for the trace debugger. No record is serialized —
    /// records exist only for the session that produced them, which is the honest current capability.
    /// This register is deliberately duplicated by the shipped-catalog replay suite in
    /// Iterate.Infrastructure.Tests, because the Editor assembly cannot reference test assemblies and
    /// hoisting shared fixture data into a shipping runtime assembly was rejected. Each fixture below
    /// names its test-side twin so the duplication stays visible rather than silent; if one side is
    /// changed the other must change with it.
    /// </summary>
    public static class DiagnosticFixtureRegistry
    {
        /// <summary>
        /// The one-based position of the designated final Core output in the Design §28.9 Core.
        /// </summary>
        private const int DesignatedFinalOutput = 12;

        /// <summary>
        /// The instance identity the shipped Process rule is configured under.
        /// </summary>
        private const int RuleInstance = 900;

        /// <summary>
        /// The fixture names, in register order.
        /// </summary>
        /// <returns>The names the window offers.</returns>
        public static IReadOnlyList<string> Names()
        {
            return new[]
            {
                "F1 Minimal Core",
                "F2 Thermal Throttle",
                "F3 Burst Boundary Lock",
                "F4 Patch Dense",
                "F5 Align Parity (odd)",
                "F5 Align Parity (even)"
            };
        }

        /// <summary>
        /// Builds the named fixture's request from the loaded catalog. A missing definition or a
        /// malformed composition surfaces as the request's own ArgumentException, which the caller
        /// reports as a fixture defect rather than as a diagnostic result.
        /// </summary>
        /// <param name="name">The fixture name, from <see cref="Names"/>.</param>
        /// <param name="catalog">The loaded frozen catalog.</param>
        /// <returns>The assembled request.</returns>
        public static ExecutionRequest Build(string name, ContentCatalog catalog)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("A fixture name is required.", nameof(name));

            if (catalog == null)
                throw new ArgumentException("A loaded catalog is required.", nameof(catalog));

            switch (name)
            {
                case "F1 Minimal Core":
                    return MinimalCore();

                case "F2 Thermal Throttle":
                    return ThrottledCore(catalog);

                case "F3 Burst Boundary Lock":
                    return BurstOrderSignature(catalog);

                case "F4 Patch Dense":
                    return PatchDense(catalog);

                case "F5 Align Parity (odd)":
                    return ParityFlip(catalog, 1);

                case "F5 Align Parity (even)":
                    return ParityFlip(catalog, 2);

                default:
                    throw new ArgumentException("Unknown fixture name: " + name, nameof(name));
            }
        }

        /// <summary>
        /// F1 — the baseline sanity fixture: Core Value = 1, then Core Score += Value. Value 1, Score 1.
        /// Test-side twin: ShippedCatalogReplayTests.F1_MinimalCore_ReplaysToMatch.
        /// </summary>
        /// <returns>The request.</returns>
        private static ExecutionRequest MinimalCore()
        {
            SourceArrangement arrangement = new(new List<SourceSlot>
            {
                SourceSlot.ForCore(new SourcePosition(1), AssignConstant("core-01", CoreRegister.Value, 1)),
                SourceSlot.ForCore(new SourcePosition(2), AddRegister("core-02", CoreRegister.Score, CoreRegister.Value))
            });

            return Request(arrangement, NoPragmas(), StandardConfiguration());
        }

        /// <summary>
        /// F2 — the Design §28.9 Core under WB-PRC-001 THERMAL THROTTLE with six WB-INS-005
        /// multiplications and no rescuer: the throttled run. Value 16, Score 24, four interventions.
        /// This is the Heat-rich fixture. Test-side twin:
        /// ShippedCatalogReplayTests.F2_DesignCoreUnderThermalThrottle_ReplaysToMatch.
        /// </summary>
        /// <param name="catalog">The loaded catalog.</param>
        /// <returns>The request.</returns>
        private static ExecutionRequest ThrottledCore(ContentCatalog catalog)
        {
            List<SourceSlot> slots = new()
            {
                SourceSlot.ForCore(new SourcePosition(1), AssignConstant("core-01", CoreRegister.Value, 1)),
                SourceSlot.ForCore(new SourcePosition(2), AssignConstant("core-02", CoreRegister.Signal, 0))
            };

            for (int index = 0; index < 6; index++)
            {
                slots.Add(SourceSlot.ForInstruction(new SourcePosition(3 + index), Instruction(catalog, "WB-INS-005", 10 + index)));
            }

            slots.Add(SourceSlot.ForCore(new SourcePosition(9), MultiplyConstant("core-09", CoreRegister.Value, 2)));
            slots.Add(SourceSlot.ForCore(new SourcePosition(10), AddRegister("core-10", CoreRegister.Score, CoreRegister.Value)));
            slots.Add(SourceSlot.ForCore(new SourcePosition(11), MultiplyConstant("core-11", CoreRegister.Value, 2)));
            slots.Add(SourceSlot.ForCore(new SourcePosition(DesignatedFinalOutput), AddRegister("core-12", CoreRegister.Score, CoreRegister.Value)));

            return Request(new SourceArrangement(slots), NoPragmas(), ThrottleConfiguration(catalog));
        }

        /// <summary>
        /// F3 — the BURST OUTPUT boundary lock: Core Value = 3, a WB-INS-012 Score host BURST locks, and
        /// a trailing Core Score += Value that reads the ALIGN-adjusted Value. Value 4, Score 10.
        /// Test-side twin: ShippedCatalogReplayTests.F3_BurstOutputBoundaryLock_ReplaysToMatch.
        /// </summary>
        /// <param name="catalog">The loaded catalog.</param>
        /// <returns>The request.</returns>
        private static ExecutionRequest BurstOrderSignature(ContentCatalog catalog)
        {
            SourceArrangement arrangement = new(new List<SourceSlot>
            {
                SourceSlot.ForCore(new SourcePosition(1), AssignConstant("core-01", CoreRegister.Value, 3)),
                SourceSlot.ForInstruction(new SourcePosition(2), Instruction(catalog, "WB-INS-012", 20)),
                SourceSlot.ForCore(new SourcePosition(3), AddRegister("core-03", CoreRegister.Score, CoreRegister.Value))
            });

            return Request(arrangement, Pragmas(catalog, "WB-DIR-003", 900, "WB-DIR-002", 950), StandardConfiguration());
        }

        /// <summary>
        /// F4 — the Patch-dense composition: all six frozen WB-PAT records across six distinct hosts,
        /// every host-and-Patch pairing taken from a standing green single-Patch test. This is the
        /// near-miss-rich fixture, and the one to debug when checking that near-misses render their
        /// failed requirement. Its registers are deliberately not hand-traced, because no standing suite
        /// composes all six at once. Test-side twin:
        /// ShippedCatalogReplayTests.F4_PatchDense_ReplaysToMatch.
        /// </summary>
        /// <param name="catalog">The loaded catalog.</param>
        /// <returns>The request.</returns>
        private static ExecutionRequest PatchDense(ContentCatalog catalog)
        {
            StructureInstance condition = Structure(catalog, "WB-STR-002", 110);
            SourceArrangement arrangement = new(new List<SourceSlot>
            {
                SourceSlot.ForInstruction(new SourcePosition(1), Patched(catalog, "WB-INS-002", 10, "WB-PAT-001", 60)),
                SourceSlot.ForInstruction(new SourcePosition(2), Patched(catalog, "WB-INS-012", 20, "WB-PAT-002", 61)),
                SourceSlot.ForInstruction(new SourcePosition(3), Patched(catalog, "WB-INS-002", 11, "WB-PAT-006", 62)),
                SourceSlot.ForInstruction(new SourcePosition(4), Patched(catalog, "WB-INS-002", 12, "WB-PAT-003", 63)),
                SourceSlot.ForInstruction(new SourcePosition(5), Patched(catalog, "WB-INS-012", 21, "WB-PAT-005", 64)),
                SourceSlot.ForStructureHeader(new SourcePosition(6), condition),
                SourceSlot.ForContainedInstruction(new SourcePosition(7), condition, Patched(catalog, "WB-INS-002", 13, "WB-PAT-004", 65))
            });

            return Request(arrangement, NoPragmas(), StandardConfiguration());
        }

        /// <summary>
        /// F5 — the ALIGN parity pair: a FEEDBACK-patched Score host under BURST OUTPUT and ALIGN, whose
        /// Burst branch flips the parity the fresh ALIGN offer reads. The odd seed qualifies
        /// (Value 4, Score 3); the even seed near-misses exactly once (Value 4, Score 5). Test-side
        /// twins: ShippedCatalogReplayTests.F5_AlignParityOddSeed_ReplaysToMatch and
        /// F5_AlignParityEvenSeed_ReplaysToMatch.
        /// </summary>
        /// <param name="catalog">The loaded catalog.</param>
        /// <param name="seed">The Core-assigned starting Value.</param>
        /// <returns>The request.</returns>
        private static ExecutionRequest ParityFlip(ContentCatalog catalog, int seed)
        {
            SourceArrangement arrangement = new(new List<SourceSlot>
            {
                SourceSlot.ForCore(new SourcePosition(1), AssignConstant("core-01", CoreRegister.Value, seed)),
                SourceSlot.ForInstruction(new SourcePosition(2), Patched(catalog, "WB-INS-012", 10, "WB-PAT-005", 60))
            });

            return Request(arrangement, Pragmas(catalog, "WB-DIR-003", 900, "WB-DIR-002", 950), StandardConfiguration());
        }

        /// <summary>
        /// Assembles a request over the arrangement with no installed Dependencies and an initial
        /// compilation at zero cost.
        /// </summary>
        /// <param name="arrangement">The source arrangement.</param>
        /// <param name="pragmas">The active Directive pragmas.</param>
        /// <param name="configuration">The Process execution configuration.</param>
        /// <returns>The assembled request.</returns>
        private static ExecutionRequest Request(
            SourceArrangement arrangement,
            List<DirectiveInstance> pragmas,
            ProcessExecutionConfiguration configuration)
        {
            CompiledSource source = new(
                arrangement,
                pragmas,
                new CompilationCostBreakdown(CompilationClassification.Initial, 0, true, 0, new List<CostModifierEntry>(), 0, false));

            return new ExecutionRequest(source, configuration, StandardStamps(), ZeroState(), new List<DependencyInstance>());
        }

        /// <summary>
        /// The standard configuration carrying the seven header identities and the §28.3 Score bands,
        /// with no Process rule and no designated final Core output.
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
        /// The configuration carrying the shipped Process rule, the Balance §6.4 Score bands, and line
        /// twelve as the designated final Core output.
        /// </summary>
        /// <param name="catalog">The loaded catalog supplying the Process rule.</param>
        /// <returns>The configuration.</returns>
        private static ProcessExecutionConfiguration ThrottleConfiguration(ContentCatalog catalog)
        {
            if (!catalog.TryGetProcessRule(new ProcessRuleID("WB-PRC-001"), out ProcessRuleDefinition definition))
                throw new ArgumentException("The catalog does not define WB-PRC-001.", nameof(catalog));

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
        /// <param name="catalog">The loaded catalog.</param>
        /// <param name="id">The Instruction's surrogate-key identity.</param>
        /// <param name="instance">The instance identity value.</param>
        /// <returns>The Instruction instance.</returns>
        private static InstructionInstance Instruction(ContentCatalog catalog, string id, int instance)
        {
            if (!catalog.TryGetInstruction(new InstructionID(id), out InstructionDefinition definition))
                throw new ArgumentException("The catalog does not define " + id + ".", nameof(id));

            return new InstructionInstance(new InstanceID(instance), definition, null);
        }

        /// <summary>
        /// Wraps a real frozen Instruction definition in an instance carrying a socketed Patch.
        /// </summary>
        /// <param name="catalog">The loaded catalog.</param>
        /// <param name="instructionID">The host Instruction's surrogate-key identity.</param>
        /// <param name="hostInstance">The host instance identity value.</param>
        /// <param name="patchID">The Patch's surrogate-key identity.</param>
        /// <param name="patchInstance">The Patch instance identity value.</param>
        /// <returns>The patched Instruction instance.</returns>
        private static InstructionInstance Patched(
            ContentCatalog catalog,
            string instructionID,
            int hostInstance,
            string patchID,
            int patchInstance)
        {
            if (!catalog.TryGetInstruction(new InstructionID(instructionID), out InstructionDefinition definition))
                throw new ArgumentException("The catalog does not define " + instructionID + ".", nameof(instructionID));

            if (!catalog.TryGetPatch(new PatchID(patchID), out PatchDefinition patch))
                throw new ArgumentException("The catalog does not define " + patchID + ".", nameof(patchID));

            return new InstructionInstance(new InstanceID(hostInstance), definition, new PatchInstance(new InstanceID(patchInstance), patch));
        }

        /// <summary>
        /// Wraps a real frozen Structure definition in an instance.
        /// </summary>
        /// <param name="catalog">The loaded catalog.</param>
        /// <param name="id">The Structure's surrogate-key identity.</param>
        /// <param name="instance">The instance identity value.</param>
        /// <returns>The Structure instance.</returns>
        private static StructureInstance Structure(ContentCatalog catalog, string id, int instance)
        {
            if (!catalog.TryGetStructure(new StructureID(id), out StructureDefinition definition))
                throw new ArgumentException("The catalog does not define " + id + ".", nameof(id));

            return new StructureInstance(new InstanceID(instance), definition);
        }

        /// <summary>
        /// Wraps a real frozen Directive definition in a pragma instance.
        /// </summary>
        /// <param name="catalog">The loaded catalog.</param>
        /// <param name="id">The Directive's surrogate-key identity.</param>
        /// <param name="instance">The instance identity value.</param>
        /// <returns>The Directive instance.</returns>
        private static DirectiveInstance Directive(ContentCatalog catalog, string id, int instance)
        {
            if (!catalog.TryGetDirective(new DirectiveID(id), out DirectiveDefinition definition))
                throw new ArgumentException("The catalog does not define " + id + ".", nameof(id));

            return new DirectiveInstance(new InstanceID(instance), definition);
        }

        /// <summary>
        /// Two real frozen Directives wrapped as a pragma list, in the given order.
        /// </summary>
        /// <param name="catalog">The loaded catalog.</param>
        /// <param name="firstID">The first Directive's surrogate-key identity.</param>
        /// <param name="firstInstance">The first instance identity value.</param>
        /// <param name="secondID">The second Directive's surrogate-key identity.</param>
        /// <param name="secondInstance">The second instance identity value.</param>
        /// <returns>The pragma list.</returns>
        private static List<DirectiveInstance> Pragmas(
            ContentCatalog catalog,
            string firstID,
            int firstInstance,
            string secondID,
            int secondInstance)
        {
            return new List<DirectiveInstance>
            {
                Directive(catalog, firstID, firstInstance),
                Directive(catalog, secondID, secondInstance)
            };
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
        /// The standard reproduction revision stamps: the content catalog and the random service.
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
    }
}