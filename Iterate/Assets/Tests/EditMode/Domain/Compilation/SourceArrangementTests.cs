using System;
using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Domain.Compilation;
using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Domain.Compilation.Tests
{
    /// <summary>
    /// Tests construction validation, lookup, and functional update of <see cref="SourceArrangement"/>
    /// over the Design §5 standard Core shape (positions 01–02 Core, 03–08 empty player, 09 Core):
    /// contiguous one-based numbering, Structure footprint and ownership rules, no duplicate Structure,
    /// no nesting, and that <see cref="SourceArrangement.WithSlots"/> leaves the original untouched.
    /// </summary>
    public sealed class SourceArrangementTests
    {
        private static readonly CoreLine _coreOne = new("core-01", new CoreLineOperation(CoreLineOperator.Assign, CoreRegister.Value, OperandSpec.FromConstant(1)));
        private static readonly CoreLine _coreTwo = new("core-02", new CoreLineOperation(CoreLineOperator.Assign, CoreRegister.Signal, OperandSpec.FromConstant(0)));
        private static readonly CoreLine _coreNine = new("core-09", new CoreLineOperation(CoreLineOperator.Add, CoreRegister.Score, OperandSpec.FromRegister(CoreRegister.Value)));

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
        public void Constructor_StandardCoreShape_Constructs()
        {
            SourceArrangement arrangement = StandardCore();

            Assert.AreEqual(9, arrangement.SlotCount);
            Assert.AreEqual(9, arrangement.Slots.Count);
        }

        [Test]
        public void SlotAt_KnownPosition_ReturnsSlot()
        {
            SourceArrangement arrangement = StandardCore();

            SourceSlot slot = arrangement.SlotAt(new SourcePosition(9));

            Assert.AreEqual(SourceSlotKind.Core, slot.Kind);
            Assert.AreEqual(_coreNine, slot.Core);
        }

        [Test]
        public void SlotAt_AbsentPosition_Throws()
        {
            SourceArrangement arrangement = StandardCore();

            Assert.Throws<ArgumentException>(() => _ = arrangement.SlotAt(new SourcePosition(10)));
        }

        [Test]
        public void Constructor_GapInNumbering_Throws()
        {
            List<SourceSlot> slots = new()
            {
                SourceSlot.ForCore(new SourcePosition(1), _coreOne),
                SourceSlot.ForEmpty(new SourcePosition(2)),
                SourceSlot.ForEmpty(new SourcePosition(4))
            };

            Assert.Throws<ArgumentException>(() => _ = new SourceArrangement(slots));
        }

        [Test]
        public void Constructor_NotStartingAtOne_Throws()
        {
            List<SourceSlot> slots = new()
            {
                SourceSlot.ForCore(new SourcePosition(2), _coreOne),
                SourceSlot.ForEmpty(new SourcePosition(3))
            };

            Assert.Throws<ArgumentException>(() => _ = new SourceArrangement(slots));
        }

        [Test]
        public void Constructor_StructureHeaderWithWrongContainedCount_Throws()
        {
            StructureInstance structure = new(new InstanceID(1), _repeatDefinition);
            List<SourceSlot> slots = new()
            {
                SourceSlot.ForCore(new SourcePosition(1), _coreOne),
                SourceSlot.ForStructureHeader(new SourcePosition(2), structure),
                SourceSlot.ForEmpty(new SourcePosition(3))
            };

            Assert.Throws<ArgumentException>(() => _ = new SourceArrangement(slots));
        }

        [Test]
        public void Constructor_ContainedOwnedByDifferentStructure_Throws()
        {
            StructureInstance header = new(new InstanceID(1), _repeatDefinition);
            StructureInstance other = new(new InstanceID(2), _repeatDefinition);
            List<SourceSlot> slots = new()
            {
                SourceSlot.ForCore(new SourcePosition(1), _coreOne),
                SourceSlot.ForStructureHeader(new SourcePosition(2), header),
                SourceSlot.ForContainedEmpty(new SourcePosition(3), other)
            };

            Assert.Throws<ArgumentException>(() => _ = new SourceArrangement(slots));
        }

        [Test]
        public void Constructor_ContainedSlotWithoutHeader_Throws()
        {
            StructureInstance structure = new(new InstanceID(1), _repeatDefinition);
            List<SourceSlot> slots = new()
            {
                SourceSlot.ForCore(new SourcePosition(1), _coreOne),
                SourceSlot.ForContainedEmpty(new SourcePosition(2), structure),
                SourceSlot.ForEmpty(new SourcePosition(3))
            };

            Assert.Throws<ArgumentException>(() => _ = new SourceArrangement(slots));
        }

        [Test]
        public void Constructor_DuplicateStructure_Throws()
        {
            StructureInstance structure = new(new InstanceID(1), _repeatDefinition);
            List<SourceSlot> slots = new()
            {
                SourceSlot.ForStructureHeader(new SourcePosition(1), structure),
                SourceSlot.ForContainedEmpty(new SourcePosition(2), structure),
                SourceSlot.ForStructureHeader(new SourcePosition(3), structure),
                SourceSlot.ForContainedEmpty(new SourcePosition(4), structure)
            };

            Assert.Throws<ArgumentException>(() => _ = new SourceArrangement(slots));
        }

        [Test]
        public void Constructor_ValidStructureSpan_Constructs()
        {
            SourceArrangement arrangement = WithStructureAt3(new StructureInstance(new InstanceID(1), _repeatDefinition));

            Assert.AreEqual(SourceSlotKind.StructureHeader, arrangement.SlotAt(new SourcePosition(3)).Kind);
            Assert.AreEqual(SourceSlotKind.ContainedEmpty, arrangement.SlotAt(new SourcePosition(4)).Kind);
        }

        [Test]
        public void TryFindInstance_InstructionOccupant_Found()
        {
            InstructionInstance instruction = new(new InstanceID(7), _instructionDefinition, null);
            SourceArrangement arrangement = WithInstructionAt3(instruction);

            bool found = arrangement.TryFindInstance(new InstanceID(7), out SourceSlot slot);

            Assert.IsTrue(found);
            Assert.AreEqual(new SourcePosition(3), slot.Position);
        }

        [Test]
        public void TryFindInstance_StructureOwner_Found()
        {
            StructureInstance structure = new(new InstanceID(4), _repeatDefinition);
            SourceArrangement arrangement = WithStructureAt3(structure);

            bool found = arrangement.TryFindInstance(new InstanceID(4), out SourceSlot slot);

            Assert.IsTrue(found);
            Assert.AreEqual(new SourcePosition(3), slot.Position);
        }

        [Test]
        public void TryFindInstance_Absent_ReturnsFalse()
        {
            SourceArrangement arrangement = StandardCore();

            bool found = arrangement.TryFindInstance(new InstanceID(99), out SourceSlot slot);

            Assert.IsFalse(found);
            Assert.IsNull(slot);
        }

        [Test]
        public void WithSlots_ReplacesByPosition_ReturnsNewInstanceLeavingOriginalUntouched()
        {
            SourceArrangement original = StandardCore();
            InstructionInstance instruction = new(new InstanceID(7), _instructionDefinition, null);

            SourceArrangement updated = original.WithSlots(new List<SourceSlot>
            {
                SourceSlot.ForInstruction(new SourcePosition(3), instruction)
            });

            Assert.AreNotSame(original, updated);
            Assert.AreEqual(SourceSlotKind.Instruction, updated.SlotAt(new SourcePosition(3)).Kind);
            Assert.AreEqual(SourceSlotKind.Empty, original.SlotAt(new SourcePosition(3)).Kind);
        }

        [Test]
        public void WithSlots_InvalidReplacement_Throws()
        {
            SourceArrangement original = StandardCore();
            StructureInstance structure = new(new InstanceID(1), _repeatDefinition);

            Assert.Throws<ArgumentException>(() => _ = original.WithSlots(new List<SourceSlot>
            {
                SourceSlot.ForStructureHeader(new SourcePosition(3), structure)
            }));
        }

        private static SourceArrangement StandardCore()
        {
            return new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForCore(new SourcePosition(1), _coreOne),
                SourceSlot.ForCore(new SourcePosition(2), _coreTwo),
                SourceSlot.ForEmpty(new SourcePosition(3)),
                SourceSlot.ForEmpty(new SourcePosition(4)),
                SourceSlot.ForEmpty(new SourcePosition(5)),
                SourceSlot.ForEmpty(new SourcePosition(6)),
                SourceSlot.ForEmpty(new SourcePosition(7)),
                SourceSlot.ForEmpty(new SourcePosition(8)),
                SourceSlot.ForCore(new SourcePosition(9), _coreNine)
            });
        }

        private static SourceArrangement WithInstructionAt3(InstructionInstance instruction)
        {
            return new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForCore(new SourcePosition(1), _coreOne),
                SourceSlot.ForCore(new SourcePosition(2), _coreTwo),
                SourceSlot.ForInstruction(new SourcePosition(3), instruction),
                SourceSlot.ForEmpty(new SourcePosition(4)),
                SourceSlot.ForEmpty(new SourcePosition(5)),
                SourceSlot.ForEmpty(new SourcePosition(6)),
                SourceSlot.ForEmpty(new SourcePosition(7)),
                SourceSlot.ForEmpty(new SourcePosition(8)),
                SourceSlot.ForCore(new SourcePosition(9), _coreNine)
            });
        }

        private static SourceArrangement WithStructureAt3(StructureInstance structure)
        {
            return new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForCore(new SourcePosition(1), _coreOne),
                SourceSlot.ForCore(new SourcePosition(2), _coreTwo),
                SourceSlot.ForStructureHeader(new SourcePosition(3), structure),
                SourceSlot.ForContainedEmpty(new SourcePosition(4), structure),
                SourceSlot.ForEmpty(new SourcePosition(5)),
                SourceSlot.ForEmpty(new SourcePosition(6)),
                SourceSlot.ForEmpty(new SourcePosition(7)),
                SourceSlot.ForEmpty(new SourcePosition(8)),
                SourceSlot.ForCore(new SourcePosition(9), _coreNine)
            });
        }
    }
}
