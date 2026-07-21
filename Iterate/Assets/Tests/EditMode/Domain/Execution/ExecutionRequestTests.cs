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
    /// Tests that <see cref="ExecutionRequest"/> validates its four components fully at construction and
    /// enforces the walking-skeleton content contract: an arrangement carrying any Structure-bearing slot
    /// is rejected at the boundary rather than silently mis-executed.
    /// </summary>
    public sealed class ExecutionRequestTests
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

        private static readonly StructureDefinition _repeatDefinition = new(
            new StructureID("WB-STR-001"),
            "Repeat 2",
            "Repeat 2",
            ContentCategory.Structure,
            Rarity.Common,
            Array.Empty<string>(),
            2,
            StructureKind.Repeat,
            2,
            null);

        [Test]
        public void Constructor_ValidRequest_RoundTrips()
        {
            CompiledSource source = CompiledFrom(CoreAndInstructionArrangement());
            ProcessExecutionConfiguration configuration = ValidConfiguration();
            List<RevisionStamp> stamps = ValidStamps();
            InitialExecutionState initialState = ZeroInitialState();

            ExecutionRequest request = new(source, configuration, stamps, initialState);

            Assert.AreSame(source, request.Source);
            Assert.AreSame(configuration, request.Configuration);
            Assert.AreEqual(stamps, request.RevisionStamps);
            Assert.AreSame(initialState, request.InitialState);
        }

        [Test]
        public void Constructor_NullSource_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new ExecutionRequest(
                null, ValidConfiguration(), ValidStamps(), ZeroInitialState()));
        }

        [Test]
        public void Constructor_NullConfiguration_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new ExecutionRequest(
                CompiledFrom(CoreAndInstructionArrangement()), null, ValidStamps(), ZeroInitialState()));
        }

        [Test]
        public void Constructor_NullInitialState_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new ExecutionRequest(
                CompiledFrom(CoreAndInstructionArrangement()), ValidConfiguration(), ValidStamps(), null));
        }

        [Test]
        public void Constructor_NullStamps_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new ExecutionRequest(
                CompiledFrom(CoreAndInstructionArrangement()), ValidConfiguration(), null, ZeroInitialState()));
        }

        [Test]
        public void Constructor_EmptyStamps_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new ExecutionRequest(
                CompiledFrom(CoreAndInstructionArrangement()), ValidConfiguration(), new List<RevisionStamp>(), ZeroInitialState()));
        }

        [Test]
        public void Constructor_StructureBearingArrangement_Throws()
        {
            CompiledSource source = CompiledFrom(StructureBearingArrangement());

            Assert.Throws<ArgumentException>(() => _ = new ExecutionRequest(
                source, ValidConfiguration(), ValidStamps(), ZeroInitialState()));
        }

        private static ProcessThresholds ValidThresholds()
        {
            return new ProcessThresholds(new ScoreValue(20), new ScoreValue(30), new ScoreValue(36));
        }

        private static ProcessExecutionConfiguration ValidConfiguration()
        {
            return new ProcessExecutionConfiguration(
                "exec",
                "compilation",
                "source-rev",
                "process",
                "core",
                "rule-config",
                "session-seed",
                ValidThresholds());
        }

        private static List<RevisionStamp> ValidStamps()
        {
            return new List<RevisionStamp>
            {
                new RevisionStamp("Content Catalog", "0.1.0"),
                new RevisionStamp("Random Service", "iterate-rng-1")
            };
        }

        private static InitialExecutionState ZeroInitialState()
        {
            return new InitialExecutionState(new ValueAmount(0), new SignalValue(0), new ScoreValue(0));
        }

        private static CompiledSource CompiledFrom(SourceArrangement arrangement)
        {
            return new CompiledSource(
                arrangement,
                new List<DirectiveInstance>(),
                new CompilationCostBreakdown(CompilationClassification.Initial, 0, true, 0, new List<CostModifierEntry>(), 0, false));
        }

        private static SourceArrangement CoreAndInstructionArrangement()
        {
            CoreLine core = new("core-01", new CoreLineOperation(CoreLineOperator.Assign, CoreRegister.Value, OperandSpec.FromConstant(1)));
            InstructionInstance instruction = new(new InstanceID(1), _instructionDefinition, null);

            return new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForCore(new SourcePosition(1), core),
                SourceSlot.ForInstruction(new SourcePosition(2), instruction),
                SourceSlot.ForEmpty(new SourcePosition(3))
            });
        }

        private static SourceArrangement StructureBearingArrangement()
        {
            CoreLine core = new("core-01", new CoreLineOperation(CoreLineOperator.Assign, CoreRegister.Value, OperandSpec.FromConstant(1)));
            StructureInstance structure = new(new InstanceID(4), _repeatDefinition);

            return new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForCore(new SourcePosition(1), core),
                SourceSlot.ForStructureHeader(new SourcePosition(2), structure),
                SourceSlot.ForContainedEmpty(new SourcePosition(3), structure)
            });
        }
    }
}
