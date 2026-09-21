using System;
using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Domain.Compilation;
using Iterate.Domain.Content;
using Iterate.Domain.Execution;
using Iterate.Domain.Trace;
using Iterate.Domain.Values;

namespace Iterate.Infrastructure.Content.Tests
{
    /// <summary>
    /// The F1-F5 fixture register, built against the real frozen catalog. Extracted so the replay suite
    /// and the record-codec suite drive the same six requests rather than each carrying its own copy:
    /// the register is already duplicated once by the Editor-side diagnostic fixture registry, which
    /// cannot reference a test assembly, and a third copy would put drift somewhere nobody is looking.
    ///
    /// Every lookup asserts rather than returning null, so a fixture naming content the shipped catalog
    /// has stopped carrying fails where it is built instead of much later as an unexplained divergence.
    /// </summary>
    internal static class ShippedCatalogFixtureRequests
    {
        /// <summary>
        /// The one-based position of the designated final Core output in the Design section 28.9 Core.
        /// </summary>
        private const int DesignatedFinalOutput = 12;

        /// <summary>
        /// The instance identity the shipped Process rule is configured under.
        /// </summary>
        private const int RuleInstance = 900;

        /// <summary>
        /// The six fixtures in register order, each paired with the baseline slug its document is named
        /// by, so a caller can write one file per fixture without inventing its own naming.
        /// </summary>
        /// <param name="catalog">The frozen shipped catalog.</param>
        /// <returns>The register, in order.</returns>
        internal static IReadOnlyList<ShippedCatalogFixture> All(ContentCatalog catalog)
        {
            return new List<ShippedCatalogFixture>
            {
                new("F1 Minimal Core", MinimalCore(catalog)),
                new("F2 Design Core Under Thermal Throttle", ThrottledCore(catalog)),
                new("F3 Burst Output Boundary Lock", BurstOrderSignature(catalog)),
                new("F4 Patch Dense", PatchDense(catalog)),
                new("F5 Align Parity (odd seed)", ParityFlip(catalog, 3)),
                new("F5 Align Parity (even seed)", ParityFlip(catalog, 4))
            };
        }

        internal static ExecutionRequest MinimalCore(ContentCatalog catalog)
        {
            SourceArrangement arrangement = new(new List<SourceSlot>
            {
                SourceSlot.ForCore(new SourcePosition(1), AssignConstant("core-01", CoreRegister.Value, 1)),
                SourceSlot.ForCore(new SourcePosition(2), AddRegister("core-02", CoreRegister.Score, CoreRegister.Value))
            });

            return Request(arrangement, NoDependencies(), NoPragmas(), StandardConfiguration());
        }

        internal static ExecutionRequest ThrottledCore(ContentCatalog catalog)
        {
            List<SourceSlot> slots = new()
            {
                SourceSlot.ForCore(new SourcePosition(1), AssignConstant("core-01", CoreRegister.Value, 1)),
                SourceSlot.ForCore(new SourcePosition(2), AssignConstant("core-02", CoreRegister.Signal, 0))
            };

            for (int i = 0; i < 6; i++)
            {
                slots.Add(SourceSlot.ForInstruction(new SourcePosition(3 + i), Instruction(catalog, "WB-INS-005", 10 + i)));
            }

            slots.Add(SourceSlot.ForCore(new SourcePosition(9), MultiplyConstant("core-09", CoreRegister.Value, 2)));
            slots.Add(SourceSlot.ForCore(new SourcePosition(10), AddRegister("core-10", CoreRegister.Score, CoreRegister.Value)));
            slots.Add(SourceSlot.ForCore(new SourcePosition(11), MultiplyConstant("core-11", CoreRegister.Value, 2)));
            slots.Add(SourceSlot.ForCore(new SourcePosition(DesignatedFinalOutput), AddRegister("core-12", CoreRegister.Score, CoreRegister.Value)));

            return Request(new SourceArrangement(slots), NoDependencies(), NoPragmas(), ThrottleConfiguration(catalog));
        }

        internal static ExecutionRequest BurstOrderSignature(ContentCatalog catalog)
        {
            SourceArrangement arrangement = new(new List<SourceSlot>
            {
                SourceSlot.ForCore(new SourcePosition(1), AssignConstant("core-01", CoreRegister.Value, 3)),
                SourceSlot.ForInstruction(new SourcePosition(2), Instruction(catalog, "WB-INS-012", 20)),
                SourceSlot.ForCore(new SourcePosition(3), AddRegister("core-03", CoreRegister.Score, CoreRegister.Value))
            });

            return Request(arrangement, NoDependencies(), Pragmas(catalog, "WB-DIR-003", 900, "WB-DIR-002", 950), StandardConfiguration());
        }

        internal static ExecutionRequest PatchDense(ContentCatalog catalog)
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

            return Request(arrangement, NoDependencies(), NoPragmas(), StandardConfiguration());
        }

        internal static ExecutionRequest ParityFlip(ContentCatalog catalog, int seed)
        {
            SourceArrangement arrangement = new(new List<SourceSlot>
            {
                SourceSlot.ForCore(new SourcePosition(1), AssignConstant("core-01", CoreRegister.Value, seed)),
                SourceSlot.ForInstruction(new SourcePosition(2), Patched(catalog, "WB-INS-012", 10, "WB-PAT-005", 60))
            });

            return Request(arrangement, NoDependencies(), Pragmas(catalog, "WB-DIR-003", 900, "WB-DIR-002", 950), StandardConfiguration());
        }

        internal static ProcessExecutionConfiguration ThrottleConfiguration(ContentCatalog catalog)
        {
            Assert.IsTrue(
                catalog.TryGetProcessRule(new ProcessRuleID("WB-PRC-001"), out ProcessRuleDefinition definition),
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

        private static InstructionInstance Instruction(ContentCatalog catalog, string id, int instance)
        {
            Assert.IsTrue(catalog.TryGetInstruction(new InstructionID(id), out InstructionDefinition definition), id);

            return new InstructionInstance(new InstanceID(instance), definition, Array.Empty<PatchAttachment>());
        }

        private static InstructionInstance Patched(
            ContentCatalog catalog,
            string instructionID,
            int hostInstance,
            string patchID,
            int patchInstance)
        {
            Assert.IsTrue(catalog.TryGetInstruction(new InstructionID(instructionID), out InstructionDefinition definition), instructionID);
            Assert.IsTrue(catalog.TryGetPatch(new PatchID(patchID), out PatchDefinition patch), patchID);

            return new InstructionInstance(new InstanceID(hostInstance), definition, new[] { new PatchAttachment(1, new PatchInstance(new InstanceID(patchInstance), patch)) });
        }

        private static StructureInstance Structure(ContentCatalog catalog, string id, int instance)
        {
            Assert.IsTrue(catalog.TryGetStructure(new StructureID(id), out StructureDefinition definition), id);

            return new StructureInstance(new InstanceID(instance), definition);
        }

        private static DirectiveInstance Directive(ContentCatalog catalog, string id, int instance)
        {
            Assert.IsTrue(catalog.TryGetDirective(new DirectiveID(id), out DirectiveDefinition definition), id);

            return new DirectiveInstance(new InstanceID(instance), definition);
        }

        private static List<DirectiveInstance> Pragmas(
            ContentCatalog catalog,
            string firstID,
            int firstInstance,
            string secondID,
            int secondInstance)
        {
            return new List<DirectiveInstance> { Directive(catalog, firstID, firstInstance), Directive(catalog, secondID, secondInstance) };
        }

        internal static ExecutionRequest Request(
            SourceArrangement arrangement,
            List<DependencyInstance> installed,
            List<DirectiveInstance> pragmas,
            ProcessExecutionConfiguration configuration)
        {
            CompiledSource source = new(
                arrangement,
                pragmas,
                new CompilationCostBreakdown(CompilationClassification.Initial, 0, true, 0, new List<CostModifierEntry>(), 0, false));

            return new ExecutionRequest(source, configuration, StandardStamps(), ZeroState(), installed);
        }

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

        private static List<RevisionStamp> StandardStamps()
        {
            return new List<RevisionStamp>
            {
                new RevisionStamp("Content Catalog", "0.1.0"),
                new RevisionStamp("Random Service", "iterate-rng-1")
            };
        }

        private static InitialExecutionState ZeroState()
        {
            return new InitialExecutionState(new ValueAmount(0), new SignalValue(0), new ScoreValue(0));
        }

        private static List<DependencyInstance> NoDependencies()
        {
            return new List<DependencyInstance>();
        }

        private static List<DirectiveInstance> NoPragmas()
        {
            return new List<DirectiveInstance>();
        }

        private static CoreLine AssignConstant(string identity, CoreRegister target, int constant)
        {
            return new CoreLine(identity, new CoreLineOperation(CoreLineOperator.Assign, target, OperandSpec.FromConstant(constant)));
        }

        private static CoreLine MultiplyConstant(string identity, CoreRegister target, int constant)
        {
            return new CoreLine(identity, new CoreLineOperation(CoreLineOperator.Multiply, target, OperandSpec.FromConstant(constant)));
        }

        private static CoreLine AddRegister(string identity, CoreRegister target, CoreRegister source)
        {
            return new CoreLine(identity, new CoreLineOperation(CoreLineOperator.Add, target, OperandSpec.FromRegister(source)));
        }

    }
}
