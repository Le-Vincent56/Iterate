using System;
using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Domain.Compilation;
using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Domain.Compilation.Tests
{
    /// <summary>
    /// Tests the pre-commit classification contract (CAB-EVT-644): while no compiled arrangement exists,
    /// <see cref="BuildState.Classify"/> returns <see cref="CompilationClassification.Initial"/> regardless
    /// of ordinary edits, Overwrites, or a manually reversed edit. The post-commit transitions between
    /// Unchanged, FreeOnlyChanged, and OrdinaryEdited are exercised once commit lands.
    /// </summary>
    public sealed class BuildStateClassificationTests
    {
        private static readonly CoreLine _coreOne = new CoreLine("core-01", new CoreLineOperation(CoreLineOperator.Assign, CoreRegister.Value, OperandSpec.FromConstant(1)));
        private static readonly CoreLine _coreTwo = new CoreLine("core-02", new CoreLineOperation(CoreLineOperator.Assign, CoreRegister.Signal, OperandSpec.FromConstant(0)));
        private static readonly CoreLine _coreNine = new CoreLine("core-09", new CoreLineOperation(CoreLineOperator.Add, CoreRegister.Score, OperandSpec.FromRegister(CoreRegister.Value)));

        private static readonly InstructionDefinition _ordinaryDefinition = new InstructionDefinition(
            new InstructionID("WB-INS-002"),
            "Signal += 2",
            "Signal += 2",
            ContentCategory.Instruction,
            Rarity.Common,
            Array.Empty<string>(),
            1,
            null,
            null,
            Array.Empty<string>());

        private static readonly InstructionDefinition _overwriteDefinition = new InstructionDefinition(
            new InstructionID("WB-INS-OVR"),
            "Value += 8",
            "Value += 8",
            ContentCategory.Instruction,
            Rarity.Common,
            new[] { "Overwrite" },
            1,
            null,
            null,
            Array.Empty<string>());

        [Test]
        public void Classify_FreshState_Initial()
        {
            BuildState state = new BuildState(StandardCore(), new FakeBuildBuffer(10));

            Assert.AreEqual(CompilationClassification.Initial, state.Classify());
        }

        [Test]
        public void Classify_AfterOrdinaryEdit_Initial()
        {
            InstructionInstance instruction = new InstructionInstance(new InstanceID(10), _ordinaryDefinition, null);
            FakeBuildBuffer buffer = new FakeBuildBuffer(10);
            buffer.AddInstruction(instruction);
            BuildState state = new BuildState(StandardCore(), buffer);

            state.TryInstall(new InstanceID(10), new SourcePosition(3));

            Assert.AreEqual(CompilationClassification.Initial, state.Classify());
        }

        [Test]
        public void Classify_AfterOverwrite_Initial()
        {
            InstructionInstance seated = new InstructionInstance(new InstanceID(11), _ordinaryDefinition, null);
            InstructionInstance overwrite = new InstructionInstance(new InstanceID(30), _overwriteDefinition, null);
            FakeBuildBuffer buffer = new FakeBuildBuffer(10);
            buffer.AddInstruction(overwrite);
            BuildState state = new BuildState(
                Player(Instr(3, seated), Empty(4), Empty(5), Empty(6), Empty(7), Empty(8)),
                buffer);

            state.TryOverwrite(new InstanceID(30), new SourcePosition(3));

            Assert.AreEqual(CompilationClassification.Initial, state.Classify());
        }

        [Test]
        public void Classify_AfterEditThenManualReversal_Initial()
        {
            InstructionInstance instruction = new InstructionInstance(new InstanceID(10), _ordinaryDefinition, null);
            FakeBuildBuffer buffer = new FakeBuildBuffer(10);
            BuildState state = new BuildState(
                Player(Instr(3, instruction), Empty(4), Empty(5), Empty(6), Empty(7), Empty(8)),
                buffer);

            state.TryMove(new SourcePosition(3), new SourcePosition(5));
            state.TryMove(new SourcePosition(5), new SourcePosition(3));

            Assert.AreEqual(CompilationClassification.Initial, state.Classify());
        }

        private static SourceSlot Empty(int line)
        {
            return SourceSlot.ForEmpty(new SourcePosition(line));
        }

        private static SourceSlot Instr(int line, InstructionInstance instruction)
        {
            return SourceSlot.ForInstruction(new SourcePosition(line), instruction);
        }

        private static SourceArrangement StandardCore()
        {
            return Player(Empty(3), Empty(4), Empty(5), Empty(6), Empty(7), Empty(8));
        }

        private static SourceArrangement Player(
            SourceSlot p3,
            SourceSlot p4,
            SourceSlot p5,
            SourceSlot p6,
            SourceSlot p7,
            SourceSlot p8)
        {
            return new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForCore(new SourcePosition(1), _coreOne),
                SourceSlot.ForCore(new SourcePosition(2), _coreTwo),
                p3,
                p4,
                p5,
                p6,
                p7,
                p8,
                SourceSlot.ForCore(new SourcePosition(9), _coreNine)
            });
        }
    }
}
