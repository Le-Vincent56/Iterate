using System;
using NUnit.Framework;
using Iterate.Domain.Compilation;
using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Domain.Compilation.Tests
{
    /// <summary>
    /// Tests that each <see cref="SourceSlot"/> static factory sets its <see cref="SourceSlotKind"/>,
    /// preserves the position, and nulls every inapplicable occupant field — so an invalid slot
    /// combination is unrepresentable.
    /// </summary>
    public sealed class SourceSlotTests
    {
        private static readonly InstructionDefinition _instructionDefinition = new(
            new InstructionID("WB-INS-002"),
            "rules",
            "Add 1 to Value",
            ContentCategory.Instruction,
            Rarity.Common,
            Array.Empty<string>(),
            1,
            null,
            null,
            Array.Empty<string>());

        private static readonly StructureDefinition _structureDefinition = new(
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

        private static readonly InstructionInstance _instruction =
            new(new InstanceID(1), _instructionDefinition, null);

        private static readonly StructureInstance _structure =
            new(new InstanceID(2), _structureDefinition);

        [Test]
        public void ForCore_SetsCoreAndNullsOccupants()
        {
            CoreLine line = new("core-01", new CoreLineOperation(CoreLineOperator.Assign, CoreRegister.Value, OperandSpec.FromConstant(1)));

            SourceSlot slot = SourceSlot.ForCore(new SourcePosition(1), line);

            Assert.AreEqual(SourceSlotKind.Core, slot.Kind);
            Assert.AreEqual(new SourcePosition(1), slot.Position);
            Assert.AreEqual(line, slot.Core);
            Assert.IsNull(slot.Instruction);
            Assert.IsNull(slot.Structure);
        }

        [Test]
        public void ForEmpty_NullsEveryOccupant()
        {
            SourceSlot slot = SourceSlot.ForEmpty(new SourcePosition(3));

            Assert.AreEqual(SourceSlotKind.Empty, slot.Kind);
            Assert.AreEqual(new SourcePosition(3), slot.Position);
            Assert.IsNull(slot.Core);
            Assert.IsNull(slot.Instruction);
            Assert.IsNull(slot.Structure);
        }

        [Test]
        public void ForInstruction_SetsInstructionAndNullsCoreAndStructure()
        {
            SourceSlot slot = SourceSlot.ForInstruction(new SourcePosition(3), _instruction);

            Assert.AreEqual(SourceSlotKind.Instruction, slot.Kind);
            Assert.AreEqual(_instruction, slot.Instruction);
            Assert.IsNull(slot.Core);
            Assert.IsNull(slot.Structure);
        }

        [Test]
        public void ForStructureHeader_SetsStructureAndNullsCoreAndInstruction()
        {
            SourceSlot slot = SourceSlot.ForStructureHeader(new SourcePosition(3), _structure);

            Assert.AreEqual(SourceSlotKind.StructureHeader, slot.Kind);
            Assert.AreEqual(_structure, slot.Structure);
            Assert.IsNull(slot.Core);
            Assert.IsNull(slot.Instruction);
        }

        [Test]
        public void ForContainedEmpty_SetsOwningStructureAndNullsCoreAndInstruction()
        {
            SourceSlot slot = SourceSlot.ForContainedEmpty(new SourcePosition(4), _structure);

            Assert.AreEqual(SourceSlotKind.ContainedEmpty, slot.Kind);
            Assert.AreEqual(_structure, slot.Structure);
            Assert.IsNull(slot.Core);
            Assert.IsNull(slot.Instruction);
        }

        [Test]
        public void ForContainedInstruction_SetsOwningStructureAndInstructionAndNullsCore()
        {
            SourceSlot slot = SourceSlot.ForContainedInstruction(new SourcePosition(4), _structure, _instruction);

            Assert.AreEqual(SourceSlotKind.ContainedInstruction, slot.Kind);
            Assert.AreEqual(_structure, slot.Structure);
            Assert.AreEqual(_instruction, slot.Instruction);
            Assert.IsNull(slot.Core);
        }
    }
}
